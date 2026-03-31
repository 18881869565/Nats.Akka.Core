using System.Collections.Concurrent;

namespace Nats.Akka.CoreV2.Manager;

/// <summary>
/// 保存主题到处理器的注册关系。
/// </summary>
public abstract class NatsTopicReceiveBase
{
    /// <summary>
    /// 普通发布订阅的主题处理器集合。
    /// 一个主题可以注册多个处理委托。
    /// </summary>
    protected readonly ConcurrentDictionary<string, SubjectHandlerRegistry> TopicSubEventDic = new(StringComparer.Ordinal);

    /// <summary>
    /// 请求响应模式的主题处理器集合。
    /// 一个主题只允许注册一个请求处理器。
    /// </summary>
    protected readonly ConcurrentDictionary<string, Func<NATS.Client.Msg, CancellationToken, ValueTask>> TopicSubRequestEventDic = new(StringComparer.Ordinal);
}
