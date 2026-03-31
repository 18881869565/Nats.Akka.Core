namespace Nats.Akka.CoreV2.Manager;

/// <summary>
/// 单个主题的内部性能监控快照。
/// 所有耗时单位均为毫秒，便于直接在控制台或日志中输出。
/// </summary>
public sealed record SubjectPerformanceSnapshot(
    string Subject,
    long EnqueuedCount,
    long ProcessedCount,
    long DroppedCount,
    long CurrentQueueDepth,
    long MaxQueueDepth,
    double AverageQueueWaitMs,
    double MaxQueueWaitMs,
    double AverageDeserializeMs,
    double MaxDeserializeMs,
    double AverageHandlerMs,
    double MaxHandlerMs);
