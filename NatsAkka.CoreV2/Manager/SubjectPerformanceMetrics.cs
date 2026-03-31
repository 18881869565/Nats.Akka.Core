using System.Diagnostics;

namespace Nats.Akka.CoreV2.Manager;

/// <summary>
/// 单个主题的内部监控累加器。
/// 这里尽量使用原子操作，避免在高频订阅场景下引入额外锁竞争。
/// </summary>
internal sealed class SubjectPerformanceMetrics
{
    private long _enqueuedCount;
    private long _processedCount;
    private long _droppedCount;
    private long _currentQueueDepth;
    private long _maxQueueDepth;

    private long _queueWaitTotalTicks;
    private long _queueWaitMaxTicks;

    private long _deserializeCount;
    private long _deserializeTotalTicks;
    private long _deserializeMaxTicks;

    private long _handlerCount;
    private long _handlerTotalTicks;
    private long _handlerMaxTicks;

    /// <summary>
    /// 记录成功入队的消息。
    /// </summary>
    public void RecordEnqueue(long queueDepth)
    {
        Interlocked.Increment(ref _enqueuedCount);
        Interlocked.Exchange(ref _currentQueueDepth, queueDepth);
        UpdateMax(ref _maxQueueDepth, queueDepth);
    }

    /// <summary>
    /// 记录因背压被丢弃的消息。
    /// </summary>
    public void RecordDrop()
    {
        Interlocked.Increment(ref _droppedCount);
    }

    /// <summary>
    /// 记录排队耗时，并同步更新当前队列深度与已处理计数。
    /// </summary>
    public void RecordQueueWait(long elapsedTicks, long queueDepth)
    {
        Interlocked.Increment(ref _processedCount);
        Interlocked.Exchange(ref _currentQueueDepth, queueDepth);
        Interlocked.Add(ref _queueWaitTotalTicks, elapsedTicks);
        UpdateMax(ref _queueWaitMaxTicks, elapsedTicks);
    }

    /// <summary>
    /// 记录反序列化耗时。
    /// </summary>
    public void RecordDeserialize(long elapsedTicks)
    {
        Interlocked.Increment(ref _deserializeCount);
        Interlocked.Add(ref _deserializeTotalTicks, elapsedTicks);
        UpdateMax(ref _deserializeMaxTicks, elapsedTicks);
    }

    /// <summary>
    /// 记录业务处理耗时。
    /// </summary>
    public void RecordHandler(long elapsedTicks)
    {
        Interlocked.Increment(ref _handlerCount);
        Interlocked.Add(ref _handlerTotalTicks, elapsedTicks);
        UpdateMax(ref _handlerMaxTicks, elapsedTicks);
    }

    /// <summary>
    /// 生成只读快照，供控制台程序打印内部监控结果。
    /// </summary>
    public SubjectPerformanceSnapshot Snapshot(string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var queueWaitCount = Interlocked.Read(ref _processedCount);
        var deserializeCount = Interlocked.Read(ref _deserializeCount);
        var handlerCount = Interlocked.Read(ref _handlerCount);

        return new SubjectPerformanceSnapshot(
            subject,
            EnqueuedCount: Interlocked.Read(ref _enqueuedCount),
            ProcessedCount: queueWaitCount,
            DroppedCount: Interlocked.Read(ref _droppedCount),
            CurrentQueueDepth: Interlocked.Read(ref _currentQueueDepth),
            MaxQueueDepth: Interlocked.Read(ref _maxQueueDepth),
            AverageQueueWaitMs: ToAverageMilliseconds(Interlocked.Read(ref _queueWaitTotalTicks), queueWaitCount),
            MaxQueueWaitMs: ToMilliseconds(Interlocked.Read(ref _queueWaitMaxTicks)),
            AverageDeserializeMs: ToAverageMilliseconds(Interlocked.Read(ref _deserializeTotalTicks), deserializeCount),
            MaxDeserializeMs: ToMilliseconds(Interlocked.Read(ref _deserializeMaxTicks)),
            AverageHandlerMs: ToAverageMilliseconds(Interlocked.Read(ref _handlerTotalTicks), handlerCount),
            MaxHandlerMs: ToMilliseconds(Interlocked.Read(ref _handlerMaxTicks)));
    }

    private static double ToAverageMilliseconds(long totalTicks, long count)
    {
        return count <= 0 ? 0 : ToMilliseconds(totalTicks / (double)count);
    }

    private static double ToMilliseconds(long ticks)
    {
        return ToMilliseconds((double)ticks);
    }

    private static double ToMilliseconds(double ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private static void UpdateMax(ref long target, long candidate)
    {
        while (true)
        {
            var snapshot = Volatile.Read(ref target);
            if (candidate <= snapshot)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, candidate, snapshot) == snapshot)
            {
                return;
            }
        }
    }
}
