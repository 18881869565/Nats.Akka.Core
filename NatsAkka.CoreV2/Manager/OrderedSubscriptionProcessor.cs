using System.Diagnostics;
using System.Threading.Channels;
using NATS.Client;

namespace Nats.Akka.CoreV2.Manager;

/// <summary>
/// 单主题顺序消费处理器。
/// NATS 回调线程只负责快速入队，后台单 worker 负责顺序消费。
/// </summary>
internal sealed class OrderedSubscriptionProcessor : IDisposable
{
    private readonly string _subject;
    private readonly NatsTopicManagerOptions _options;
    private readonly SubjectPerformanceMetrics _metrics;
    private readonly Func<Msg, CancellationToken, ValueTask> _messageHandler;
    private readonly Channel<QueuedMessage> _channel;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly IAsyncSubscription _subscription;
    private readonly Task _processingTask;
    private long _queuedCount;
    private bool _disposed;

    public OrderedSubscriptionProcessor(
        IConnection connection,
        string subject,
        NatsTopicManagerOptions options,
        SubjectPerformanceMetrics metrics,
        Func<Msg, CancellationToken, ValueTask> messageHandler)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(messageHandler);

        _subject = subject;
        _options = options;
        _metrics = metrics;
        _messageHandler = messageHandler;
        _channel = CreateChannel(options);
        _subscription = connection.SubscribeAsync(subject, OnMessage);
        _processingTask = Task.Run(ProcessLoopAsync);
    }

    private void OnMessage(object? sender, MsgHandlerEventArgs args)
    {
        if (_disposeCts.IsCancellationRequested)
        {
            return;
        }

        var queuedMessage = new QueuedMessage(args.Message, Stopwatch.GetTimestamp());

        try
        {
            switch (_options.BackpressureMode)
            {
                case BackpressureMode.Wait:
                {
                    _channel.Writer.WriteAsync(queuedMessage, _disposeCts.Token).AsTask().GetAwaiter().GetResult();
                    var depth = Interlocked.Increment(ref _queuedCount);
                    _metrics.RecordEnqueue(depth);
                    break;
                }
                case BackpressureMode.DropIncoming:
                {
                    if (_channel.Writer.TryWrite(queuedMessage))
                    {
                        var depth = Interlocked.Increment(ref _queuedCount);
                        _metrics.RecordEnqueue(depth);
                    }
                    else
                    {
                        _metrics.RecordDrop();
                        _options.OnMessageDropped?.Invoke(_subject);
                    }

                    break;
                }
                case BackpressureMode.DropOldest:
                {
                    var queueWasFull = Volatile.Read(ref _queuedCount) >= _options.QueueCapacity;
                    if (_channel.Writer.TryWrite(queuedMessage))
                    {
                        if (queueWasFull)
                        {
                            _metrics.RecordDrop();
                            _metrics.RecordEnqueue(Volatile.Read(ref _queuedCount));
                            _options.OnMessageDropped?.Invoke(_subject);
                        }
                        else
                        {
                            var depth = Interlocked.Increment(ref _queuedCount);
                            _metrics.RecordEnqueue(depth);
                        }
                    }
                    else
                    {
                        _metrics.RecordDrop();
                        _options.OnMessageDropped?.Invoke(_subject);
                    }

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessLoopAsync()
    {
        try
        {
            await foreach (var queuedMessage in _channel.Reader.ReadAllAsync(_disposeCts.Token))
            {
                var queueDepth = InterlockedExtensions.DecrementClampToZero(ref _queuedCount);
                var queueWaitTicks = Stopwatch.GetTimestamp() - queuedMessage.EnqueuedTimestamp;
                _metrics.RecordQueueWait(queueWaitTicks, queueDepth);

                try
                {
                    await _messageHandler(queuedMessage.Message, _disposeCts.Token);
                }
                catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _options.OnHandlerError?.Invoke(_subject, ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposeCts.Cancel();
        _channel.Writer.TryComplete();
        _subscription.Unsubscribe();
        _subscription.Dispose();

        try
        {
            _processingTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _disposeCts.Dispose();
        }
    }

    private static Channel<QueuedMessage> CreateChannel(NatsTopicManagerOptions options)
    {
        var fullMode = options.BackpressureMode switch
        {
            BackpressureMode.Wait => BoundedChannelFullMode.Wait,
            BackpressureMode.DropIncoming => BoundedChannelFullMode.DropWrite,
            BackpressureMode.DropOldest => BoundedChannelFullMode.DropOldest,
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };

        return Channel.CreateBounded<QueuedMessage>(new BoundedChannelOptions(options.QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = fullMode
        });
    }

    private readonly record struct QueuedMessage(Msg Message, long EnqueuedTimestamp);
}
