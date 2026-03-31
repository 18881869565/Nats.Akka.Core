namespace Nats.Akka.CoreV2.Manager;

/// <summary>
/// 队列满载时的背压策略。
/// </summary>
public enum BackpressureMode
{
    /// <summary>
    /// 等待队列空位，优先保留消息顺序和完整性。
    /// </summary>
    Wait = 0,

    /// <summary>
    /// 丢弃当前新到消息，保留队列中已存在的数据。
    /// </summary>
    DropIncoming = 1,

    /// <summary>
    /// 丢弃最旧消息，为新消息腾出空间。
    /// </summary>
    DropOldest = 2
}
