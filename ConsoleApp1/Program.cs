using System.Diagnostics;
using System.Threading.Channels;
using System.Text;
using LegacyFactory = Nats.Akka.Core.Factory.NatsClientFactory;
using LegacyManagerBase = Nats.Akka.Core.Manager.NatsTopicManager;
using LegacyPacket = LegacySubjects.BenchmarkPacket;
using NATS.Client;
using V2BackpressureMode = Nats.Akka.CoreV2.Manager.BackpressureMode;
using V2Factory = Nats.Akka.CoreV2.Factory.NatsClientFactory;
using V2ManagerBase = Nats.Akka.CoreV2.Manager.NatsTopicManager;
using V2Options = Nats.Akka.CoreV2.Manager.NatsTopicManagerOptions;
using V2Performance = Nats.Akka.CoreV2.Manager.SubjectPerformanceSnapshot;
using V2Packet = V2Subjects.BenchmarkPacket;

var settings = new BenchmarkSettings(
    NatsUrl: "nats://127.0.0.1:4222",
    MessagesPerSecond: 200,
    DurationSeconds: 1,
    PayloadSizeBytes: 1024 * 50,
    DrainTimeout: TimeSpan.FromSeconds(30));

Console.OutputEncoding = Encoding.UTF8;
PrintSettings(settings);

BenchmarkResult legacyResult;
BenchmarkResult v2Result;

try
{
    legacyResult = await RunLegacyBenchmarkAsync(settings);
    v2Result = await RunV2BenchmarkAsync(settings);
}
catch (Exception ex)
{
    Console.WriteLine("压测执行失败：");
    Console.WriteLine(ex);
    return;
}

PrintResult(legacyResult);
PrintResult(v2Result);
PrintComparison(legacyResult, v2Result);

static async Task<BenchmarkResult> RunLegacyBenchmarkAsync(BenchmarkSettings settings)
{
    var payload = CreatePayload(settings.PayloadSizeBytes);
    await using var collector = new BenchmarkCollector("方案一: Nats.Akka.Core", settings.TotalMessages);
    var factory = new LegacyFactory();

    using var subscriberConnection = factory.CreateClient(settings.NatsUrl, $"legacy-sub-{Guid.NewGuid():N}", true);
    using var publisherConnection = factory.CreateClient(settings.NatsUrl, $"legacy-pub-{Guid.NewGuid():N}", true);

    _ = new LegacyBenchmarkSubscriber(subscriberConnection, collector.Record);
    await Task.Delay(300);

    var sendElapsed = await PublishLegacyAsync(publisherConnection, payload, settings);
    Console.WriteLine($"[方案一: Nats.Akka.Core] 发送完成，用时 {sendElapsed.TotalMilliseconds:F0} ms");

    var allReceived = await collector.WaitForCompletionAsync(settings.DrainTimeout);
    await Task.Delay(500);
    return collector.BuildResult(sendElapsed, allReceived);
}

static async Task<BenchmarkResult> RunV2BenchmarkAsync(BenchmarkSettings settings)
{
    var payload = CreatePayload(settings.PayloadSizeBytes);
    await using var collector = new BenchmarkCollector("方案二: NatsAkka.CoreV2", settings.TotalMessages);
    var factory = new V2Factory();

    using var subscriberConnection = factory.CreateClient(settings.NatsUrl, $"v2-sub-{Guid.NewGuid():N}", true);
    using var publisherConnection = factory.CreateClient(settings.NatsUrl, $"v2-pub-{Guid.NewGuid():N}", true);
    using var subscriber = new V2BenchmarkSubscriber(
        subscriberConnection,
        collector.Record,
        new V2Options
        {
            QueueCapacity = settings.TotalMessages * 2,
            BackpressureMode = V2BackpressureMode.Wait,
            OnMessageDropped = subject => Console.WriteLine($"[V2] 队列丢弃消息，主题：{subject}"),
            OnHandlerError = (subject, ex) => Console.WriteLine($"[V2] 处理异常，主题：{subject}，错误：{ex.Message}")
        });

    await Task.Delay(300);

    var sendElapsed = await PublishV2Async(publisherConnection, payload, settings);
    Console.WriteLine($"[方案二: NatsAkka.CoreV2] 发送完成，用时 {sendElapsed.TotalMilliseconds:F0} ms");

    var allReceived = await collector.WaitForCompletionAsync(settings.DrainTimeout);
    await Task.Delay(500);
    return collector.BuildResult(sendElapsed, allReceived, subscriber.GetPerformanceSnapshot());
}

static async Task<TimeSpan> PublishLegacyAsync(IConnection connection, byte[] payload, BenchmarkSettings settings)
{
    var stopwatch = Stopwatch.StartNew();
    var ticksPerMessage = Stopwatch.Frequency / (double)settings.MessagesPerSecond;

    for (var index = 0; index < settings.TotalMessages; index++)
    {
        var message = new LegacyPacket
        {
            Sequence = index + 1,
            SentAtUtc = DateTime.UtcNow,
            Payload = payload
        };

        Nats.Akka.Core.Extension.NatsConnectionExtension.Publish(connection, message);

        var nextTargetTicks = (index + 1) * ticksPerMessage;
        while (stopwatch.ElapsedTicks < nextTargetTicks)
        {
            await Task.Yield();
        }
    }

    stopwatch.Stop();
    return stopwatch.Elapsed;
}

static async Task<TimeSpan> PublishV2Async(IConnection connection, byte[] payload, BenchmarkSettings settings)
{
    var stopwatch = Stopwatch.StartNew();
    var ticksPerMessage = Stopwatch.Frequency / (double)settings.MessagesPerSecond;

    for (var index = 0; index < settings.TotalMessages; index++)
    {
        var message = new V2Packet
        {
            Sequence = index + 1,
            SentAtUtc = DateTime.UtcNow,
            Payload = payload
        };

        Nats.Akka.CoreV2.Extension.NatsConnectionExtension.Publish(connection, message);

        var nextTargetTicks = (index + 1) * ticksPerMessage;
        while (stopwatch.ElapsedTicks < nextTargetTicks)
        {
            await Task.Yield();
        }
    }

    stopwatch.Stop();
    return stopwatch.Elapsed;
}

static byte[] CreatePayload(int size)
{
    var buffer = GC.AllocateUninitializedArray<byte>(size);
    Random.Shared.NextBytes(buffer);
    return buffer;
}

static void PrintSettings(BenchmarkSettings settings)
{
    Console.WriteLine("NATS 传输对比测试");
    Console.WriteLine(new string('=', 72));
    Console.WriteLine($"NATS 地址          : {settings.NatsUrl}");
    Console.WriteLine($"发送频率           : {settings.MessagesPerSecond} 条/秒");
    Console.WriteLine($"发送时长           : {settings.DurationSeconds} 秒");
    Console.WriteLine($"总发送条数         : {settings.TotalMessages}");
    Console.WriteLine($"单条包体大小       : {settings.PayloadSizeBytes / 1024.0 / 1024.0:F2} MB");
    Console.WriteLine($"订阅回调策略       : 仅入队，不做业务逻辑");
    Console.WriteLine($"完成等待超时       : {settings.DrainTimeout.TotalSeconds:F0} 秒");
    Console.WriteLine($"V1 订阅主题        : {typeof(LegacyPacket).FullName}");
    Console.WriteLine($"V2 订阅主题        : {typeof(V2Packet).FullName}");
    Console.WriteLine(new string('=', 72));
}

static void PrintResult(BenchmarkResult result)
{
    Console.WriteLine();
    Console.WriteLine(result.ScenarioName);
    Console.WriteLine(new string('-', 72));
    Console.WriteLine($"发送耗时           : {result.SendElapsed.TotalMilliseconds:F0} ms");
    Console.WriteLine($"接收耗时           : {(result.ReceiveElapsed?.TotalMilliseconds.ToString("F0") ?? "N/A")} ms");
    Console.WriteLine($"实际发送频率       : {result.ActualSendRate:F2} 条/秒");
    Console.WriteLine($"实际接收频率       : {(result.ActualReceiveRate?.ToString("F2") ?? "N/A")} 条/秒");
    Console.WriteLine($"已接收条数         : {result.ReceivedCount}/{result.TotalSent}");
    Console.WriteLine($"接收完成           : {(result.AllReceived ? "是" : "否")}");
    Console.WriteLine($"回调最大并发数     : {result.MaxConcurrentHandlers}");
    Console.WriteLine($"外部队列峰值       : {result.MaxQueueDepth}");

    if (result.V2Performance != null)
    {
        Console.WriteLine("内部监控(V2)");
        Console.WriteLine($"  主题             : {result.V2Performance.Subject}");
        Console.WriteLine($"  已入队总数       : {result.V2Performance.EnqueuedCount}");
        Console.WriteLine($"  已处理总数       : {result.V2Performance.ProcessedCount}");
        Console.WriteLine($"  丢弃总数         : {result.V2Performance.DroppedCount}");
        Console.WriteLine($"  当前队列深度     : {result.V2Performance.CurrentQueueDepth}");
        Console.WriteLine($"  队列峰值         : {result.V2Performance.MaxQueueDepth}");
        Console.WriteLine($"  平均排队耗时     : {result.V2Performance.AverageQueueWaitMs:F2} ms");
        Console.WriteLine($"  最大排队耗时     : {result.V2Performance.MaxQueueWaitMs:F2} ms");
        Console.WriteLine($"  平均反序列化耗时 : {result.V2Performance.AverageDeserializeMs:F2} ms");
        Console.WriteLine($"  最大反序列化耗时 : {result.V2Performance.MaxDeserializeMs:F2} ms");
        Console.WriteLine($"  平均业务处理耗时 : {result.V2Performance.AverageHandlerMs:F2} ms");
        Console.WriteLine($"  最大业务处理耗时 : {result.V2Performance.MaxHandlerMs:F2} ms");
    }
}

static void PrintComparison(BenchmarkResult legacyResult, BenchmarkResult v2Result)
{
    Console.WriteLine();
    Console.WriteLine("结论对比");
    Console.WriteLine(new string('=', 72));
    Console.WriteLine($"V1 发送耗时        : {legacyResult.SendElapsed.TotalMilliseconds:F0} ms");
    Console.WriteLine($"V2 发送耗时        : {v2Result.SendElapsed.TotalMilliseconds:F0} ms");
    Console.WriteLine($"V1 接收耗时        : {(legacyResult.ReceiveElapsed?.TotalMilliseconds.ToString("F0") ?? "N/A")} ms");
    Console.WriteLine($"V2 接收耗时        : {(v2Result.ReceiveElapsed?.TotalMilliseconds.ToString("F0") ?? "N/A")} ms");
    Console.WriteLine($"V1 回调最大并发数  : {legacyResult.MaxConcurrentHandlers}");
    Console.WriteLine($"V2 回调最大并发数  : {v2Result.MaxConcurrentHandlers}");
    Console.WriteLine($"V1 外部队列峰值    : {legacyResult.MaxQueueDepth}");
    Console.WriteLine($"V2 外部队列峰值    : {v2Result.MaxQueueDepth}");
    Console.WriteLine($"V1 接收完成        : {(legacyResult.AllReceived ? "是" : "否")}");
    Console.WriteLine($"V2 接收完成        : {(v2Result.AllReceived ? "是" : "否")}");
    Console.WriteLine("说明：订阅回调只负责入队；接收耗时从收到第一条开始，到队列计数达到目标数量为止。");
}

internal sealed record BenchmarkSettings(
    string NatsUrl,
    int MessagesPerSecond,
    int DurationSeconds,
    int PayloadSizeBytes,
    TimeSpan DrainTimeout)
{
    public int TotalMessages => MessagesPerSecond * DurationSeconds;
}

internal sealed record BenchmarkResult(
    string ScenarioName,
    int TotalSent,
    int ReceivedCount,
    bool AllReceived,
    int MaxConcurrentHandlers,
    long MaxQueueDepth,
    TimeSpan SendElapsed,
    TimeSpan? ReceiveElapsed,
    V2Performance? V2Performance)
{
    public double ActualSendRate => TotalSent / Math.Max(0.001, SendElapsed.TotalSeconds);

    public double? ActualReceiveRate => ReceiveElapsed == null
        ? null
        : TotalSent / Math.Max(0.001, ReceiveElapsed.Value.TotalSeconds);
}

internal sealed class BenchmarkCollector : IAsyncDisposable
{
    private readonly string _scenarioName;
    private readonly int _expectedCount;
    private readonly TaskCompletionSource<bool> _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _receiveTimingLock = new();
    private readonly Channel<ReceivedMarker> _channel = Channel.CreateUnbounded<ReceivedMarker>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });
    private readonly Task _consumerTask;

    private int _receivedCount;
    private int _currentConcurrentHandlers;
    private int _maxConcurrentHandlers;
    private long _queuedCount;
    private long _maxQueuedCount;
    private Stopwatch? _receiveStopwatch;
    private TimeSpan? _receiveElapsed;

    public BenchmarkCollector(string scenarioName, int expectedCount)
    {
        _scenarioName = scenarioName;
        _expectedCount = expectedCount;
        _consumerTask = Task.Run(ProcessQueueAsync);
    }

    public void Record(LegacyPacket message) => RecordCore();

    public void Record(V2Packet message) => RecordCore();

    public async Task<bool> WaitForCompletionAsync(TimeSpan timeout)
    {
        var completed = await Task.WhenAny(_completionSource.Task, Task.Delay(timeout));
        return completed == _completionSource.Task && _completionSource.Task.Result;
    }

    public BenchmarkResult BuildResult(TimeSpan sendElapsed, bool allReceived, V2Performance? v2Performance = null)
    {
        return new BenchmarkResult(
            _scenarioName,
            _expectedCount,
            _receivedCount,
            allReceived,
            _maxConcurrentHandlers,
            Interlocked.Read(ref _maxQueuedCount),
            sendElapsed,
            allReceived ? _receiveElapsed : null,
            v2Performance);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _consumerTask;
    }

    private void RecordCore()
    {
        StartReceiveTimingIfNeeded();

        var currentConcurrency = Interlocked.Increment(ref _currentConcurrentHandlers);
        UpdateMaxConcurrent(currentConcurrency);

        try
        {
            if (!_channel.Writer.TryWrite(ReceivedMarker.Instance))
            {
                _channel.Writer.WriteAsync(ReceivedMarker.Instance).AsTask().GetAwaiter().GetResult();
            }

            var queueDepth = Interlocked.Increment(ref _queuedCount);
            UpdateMaxQueueDepth(queueDepth);
        }
        finally
        {
            Interlocked.Decrement(ref _currentConcurrentHandlers);
        }
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var _ in _channel.Reader.ReadAllAsync())
        {
            InterlockedExtensions.DecrementClampToZero(ref _queuedCount);

            var received = Interlocked.Increment(ref _receivedCount);
            if (received >= _expectedCount)
            {
                CompleteReceiveTimingIfNeeded();
                _completionSource.TrySetResult(true);
            }
        }
    }

    private void UpdateMaxConcurrent(int current)
    {
        while (true)
        {
            var snapshot = _maxConcurrentHandlers;
            if (current <= snapshot)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _maxConcurrentHandlers, current, snapshot) == snapshot)
            {
                return;
            }
        }
    }

    private void UpdateMaxQueueDepth(long current)
    {
        while (true)
        {
            var snapshot = Volatile.Read(ref _maxQueuedCount);
            if (current <= snapshot)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _maxQueuedCount, current, snapshot) == snapshot)
            {
                return;
            }
        }
    }

    private void StartReceiveTimingIfNeeded()
    {
        if (_receiveStopwatch != null)
        {
            return;
        }

        lock (_receiveTimingLock)
        {
            _receiveStopwatch ??= Stopwatch.StartNew();
        }
    }

    private void CompleteReceiveTimingIfNeeded()
    {
        if (_receiveStopwatch == null || _receiveElapsed != null)
        {
            return;
        }

        lock (_receiveTimingLock)
        {
            if (_receiveStopwatch != null && _receiveElapsed == null)
            {
                _receiveStopwatch.Stop();
                _receiveElapsed = _receiveStopwatch.Elapsed;
                Console.WriteLine($"[{_scenarioName}] 接收完成，用时 {_receiveElapsed.Value.TotalMilliseconds:F0} ms");
            }
        }
    }

    private readonly record struct ReceivedMarker
    {
        public static readonly ReceivedMarker Instance = new();
    }
}

internal sealed class LegacyBenchmarkSubscriber : LegacyManagerBase
{
    public LegacyBenchmarkSubscriber(IConnection connection, Action<LegacyPacket> onMessage)
        : base(connection)
    {
        NatsSub(onMessage);
    }
}

internal sealed class V2BenchmarkSubscriber : V2ManagerBase
{
    public V2BenchmarkSubscriber(IConnection connection, Action<V2Packet> onMessage, V2Options options)
        : base(connection, options)
    {
        NatsSub(onMessage);
    }

    public V2Performance? GetPerformanceSnapshot() => GetTopicPerformanceSnapshot<V2Packet>();
}

internal static class LegacySubjects
{
    internal sealed class BenchmarkPacket
    {
        public int Sequence { get; init; }

        public DateTime SentAtUtc { get; init; }

        public byte[] Payload { get; init; } = Array.Empty<byte>();
    }
}

internal static class V2Subjects
{
    internal sealed class BenchmarkPacket
    {
        public int Sequence { get; init; }

        public DateTime SentAtUtc { get; init; }

        public byte[] Payload { get; init; } = Array.Empty<byte>();
    }
}

internal static class InterlockedExtensions
{
    public static long DecrementClampToZero(ref long value)
    {
        while (true)
        {
            var current = Volatile.Read(ref value);
            if (current <= 0)
            {
                return 0;
            }

            if (Interlocked.CompareExchange(ref value, current - 1, current) == current)
            {
                return current - 1;
            }
        }
    }
}
