using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Nats.Akka.CoreV2.Internal;
using NATS.Client;

namespace Nats.Akka.CoreV2.Manager;

/// <summary>
/// 提供顺序消费能力的主题管理基类。
/// 与 V1 不同，本实现按“单主题单队列单 worker”模型处理消息。
/// </summary>
public abstract class NatsTopicManager : NatsTopicReceiveBase, IDisposable
{
    private readonly IConnection _connection;
    private readonly NatsTopicManagerOptions _options;
    private readonly ConcurrentDictionary<string, SubjectPerformanceMetrics> _topicMetrics = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<OrderedSubscriptionProcessor>> _topicProcessors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<OrderedSubscriptionProcessor>> _requestProcessors = new(StringComparer.Ordinal);
    private bool _disposed;

    protected NatsTopicManager(IConnection connection, NatsTopicManagerOptions? options = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? new NatsTopicManagerOptions();
        if (_options.QueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "QueueCapacity must be greater than zero.");
        }
    }

    public Action<Msg>? AllSubMsgAction { get; set; }

    public Func<Msg, CancellationToken, ValueTask>? AllSubMsgAsync { get; set; }

    protected void NatsSub<T>(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfDisposed();

        var subject = NatsSubjectName.For<T>();
        var metrics = _topicMetrics.GetOrAdd(subject, static _ => new SubjectPerformanceMetrics());
        var registry = TopicSubEventDic.GetOrAdd(subject, static _ => new SubjectHandlerRegistry());

        registry.Add((msg, _) =>
        {
            var deserializeStart = Stopwatch.GetTimestamp();
            var model = ConvertObjMsg<T>(msg);
            metrics.RecordDeserialize(Stopwatch.GetTimestamp() - deserializeStart);

            var handlerStart = Stopwatch.GetTimestamp();
            handler(model);
            metrics.RecordHandler(Stopwatch.GetTimestamp() - handlerStart);

            return ValueTask.CompletedTask;
        });

        EnsureTopicProcessor(subject);
    }

    protected void NatsSubAsync<T>(Func<T, CancellationToken, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfDisposed();

        var subject = NatsSubjectName.For<T>();
        var metrics = _topicMetrics.GetOrAdd(subject, static _ => new SubjectPerformanceMetrics());
        var registry = TopicSubEventDic.GetOrAdd(subject, static _ => new SubjectHandlerRegistry());

        registry.Add(async (msg, ct) =>
        {
            var deserializeStart = Stopwatch.GetTimestamp();
            var model = ConvertObjMsg<T>(msg);
            metrics.RecordDeserialize(Stopwatch.GetTimestamp() - deserializeStart);

            var handlerStart = Stopwatch.GetTimestamp();
            await handler(model, ct);
            metrics.RecordHandler(Stopwatch.GetTimestamp() - handlerStart);
        });

        EnsureTopicProcessor(subject);
    }

    protected void NatsSubRequest<TRequest>(Action<Msg, TRequest> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfDisposed();

        var subject = NatsSubjectName.For<TRequest>();
        if (!TopicSubRequestEventDic.TryAdd(subject, (msg, _) =>
            {
                action(msg, ConvertObjMsg<TRequest>(msg));
                return ValueTask.CompletedTask;
            }))
        {
            throw new InvalidOperationException($"A request handler for subject '{subject}' is already registered.");
        }

        EnsureRequestProcessor(subject);
    }

    protected void NatsSubRequestAsync<TRequest>(Func<Msg, TRequest, CancellationToken, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfDisposed();

        var subject = NatsSubjectName.For<TRequest>();
        if (!TopicSubRequestEventDic.TryAdd(subject, (msg, ct) => action(msg, ConvertObjMsg<TRequest>(msg), ct)))
        {
            throw new InvalidOperationException($"A request handler for subject '{subject}' is already registered.");
        }

        EnsureRequestProcessor(subject);
    }

    public T ConvertObjMsg<T>(Msg msg)
    {
        ArgumentNullException.ThrowIfNull(msg);
        return NatsMessageCodec.Deserialize<T>(msg.Data);
    }

    public object ConvertObjMsg(Type targetType, Msg msg)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(msg);
        return NatsMessageCodec.Deserialize(msg.Data, targetType);
    }

    public SubjectPerformanceSnapshot? GetTopicPerformanceSnapshot<T>()
    {
        var subject = NatsSubjectName.For<T>();
        return _topicMetrics.TryGetValue(subject, out var metrics)
            ? metrics.Snapshot(subject)
            : null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var processor in _topicProcessors.Values)
        {
            if (processor.IsValueCreated)
            {
                processor.Value.Dispose();
            }
        }

        foreach (var processor in _requestProcessors.Values)
        {
            if (processor.IsValueCreated)
            {
                processor.Value.Dispose();
            }
        }

        GC.SuppressFinalize(this);
    }

    private void EnsureTopicProcessor(string subject)
    {
        var metrics = _topicMetrics.GetOrAdd(subject, static _ => new SubjectPerformanceMetrics());
        _ = _topicProcessors.GetOrAdd(
            subject,
            s => new Lazy<OrderedSubscriptionProcessor>(
                () => new OrderedSubscriptionProcessor(_connection, s, _options, metrics, (msg, ct) => ProcessTopicMessageAsync(s, msg, ct)),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private void EnsureRequestProcessor(string subject)
    {
        var metrics = _topicMetrics.GetOrAdd(subject, static _ => new SubjectPerformanceMetrics());
        _ = _requestProcessors.GetOrAdd(
            subject,
            s => new Lazy<OrderedSubscriptionProcessor>(
                () => new OrderedSubscriptionProcessor(_connection, s, _options, metrics, (msg, ct) => ProcessRequestMessageAsync(s, msg, ct)),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private async ValueTask ProcessTopicMessageAsync(string subject, Msg msg, CancellationToken ct)
    {
        if (TopicSubEventDic.TryGetValue(subject, out var handlers))
        {
            foreach (var handler in handlers.Snapshot())
            {
                await handler(msg, ct);
            }
        }

        if (AllSubMsgAsync != null)
        {
            await AllSubMsgAsync(msg, ct);
        }

        AllSubMsgAction?.Invoke(msg);
    }

    private ValueTask ProcessRequestMessageAsync(string subject, Msg msg, CancellationToken ct)
    {
        if (!TopicSubRequestEventDic.TryGetValue(subject, out var handler))
        {
            return ValueTask.CompletedTask;
        }

        return handler(msg, ct);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
