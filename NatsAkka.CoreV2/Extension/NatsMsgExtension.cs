using Nats.Akka.CoreV2.Internal;
using NATS.Client;

namespace Nats.Akka.CoreV2.Extension;

/// <summary>
/// NATS 消息响应扩展方法。
/// </summary>
public static class NatsMsgExtension
{
    /// <summary>
    /// 将响应对象压缩后写回 NATS reply 主题。
    /// </summary>
    public static void RespondCompressed<T>(this Msg msg, T response) where T : class
    {
        ArgumentNullException.ThrowIfNull(msg);
        ArgumentNullException.ThrowIfNull(response);

        msg.Respond(NatsMessageCodec.Serialize(response));
    }

    /// <summary>
    /// 兼容 V1 的命名，内部仍调用压缩响应实现。
    /// </summary>
    public static void Responsed<T>(this Msg msg, T response) where T : class
    {
        msg.RespondCompressed(response);
    }
}
