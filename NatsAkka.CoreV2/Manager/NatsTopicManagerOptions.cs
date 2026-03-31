namespace Nats.Akka.CoreV2.Manager;

/// <summary>
/// NatsTopicManager 的顺序消费与背压配置。
/// </summary>
public sealed class NatsTopicManagerOptions
{
    /// <summary>
    /// 每个主题对应的本地有界队列容量。
    /// </summary>
    public int QueueCapacity { get; init; } = 4096;

    /// <summary>
    /// 队列写满时的处理策略。
    /// </summary>
    public BackpressureMode BackpressureMode { get; init; } = BackpressureMode.Wait;

    /// <summary>
    /// 发生消息丢弃时的回调，参数为主题名。
    /// </summary>
    public Action<string>? OnMessageDropped { get; init; }

    /// <summary>
    /// 业务处理异常回调，参数为主题名与异常对象。
    /// </summary>
    public Action<string, Exception>? OnHandlerError { get; init; }
}
