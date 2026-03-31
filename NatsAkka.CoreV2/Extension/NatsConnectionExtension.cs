using Nats.Akka.CoreV2.Internal;
using NATS.Client;

namespace Nats.Akka.CoreV2.Extension;

/// <summary>
/// IConnection 的压缩发布与请求扩展方法。
/// </summary>
public static class NatsConnectionExtension
{
    /// <summary>
    /// 使用消息类型全名作为主题，将对象序列化并压缩后发布。
    /// </summary>
    public static void Publish<T>(this IConnection connection, T message)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(message);

        connection.Publish(NatsSubjectName.For(message), NatsMessageCodec.Serialize(message));
    }

    /// <summary>
    /// 发送同步请求，响应体按目标类型解压和反序列化。
    /// </summary>
    public static TResponse? Request<TRequest, TResponse>(
        this IConnection connection,
        TRequest request,
        int timeoutMilliseconds = 5000)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);

        var msg = connection.Request(
            NatsSubjectName.For(request),
            NatsMessageCodec.Serialize(request),
            timeoutMilliseconds);

        return msg == null ? default : NatsMessageCodec.Deserialize<TResponse>(msg.Data);
    }

    /// <summary>
    /// 发送异步请求，响应体按目标类型解压和反序列化。
    /// </summary>
    public static async Task<TResponse?> RequestAsync<TRequest, TResponse>(
        this IConnection connection,
        TRequest request,
        int timeoutMilliseconds = 5000)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);

        var msg = await connection.RequestAsync(
            NatsSubjectName.For(request),
            NatsMessageCodec.Serialize(request),
            timeoutMilliseconds);

        return msg == null ? default : NatsMessageCodec.Deserialize<TResponse>(msg.Data);
    }
}
