using NATS.Client;

namespace Nats.Akka.CoreV2.Client;

/// <summary>
/// NATS 连接抽象，封装连接状态、发布和请求响应能力。
/// </summary>
public interface IConnect
{
    /// <summary>
    /// 当前连接状态。
    /// </summary>
    ConnectionState CurrentState { get; }

    /// <summary>
    /// 当前是否处于已连接状态。
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 建立连接。
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// 断开并释放连接。
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 连接状态变化事件。
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 获取底层 NATS 连接对象。
    /// </summary>
    IConnection GetConnection();

    /// <summary>
    /// 发布消息，主题名默认取消息类型全名。
    /// </summary>
    void Publish<T>(T message);

    /// <summary>
    /// 同步请求并等待响应。
    /// </summary>
    TResponse? Request<TRequest, TResponse>(TRequest request, int timeoutMilliseconds = 5000);

    /// <summary>
    /// 异步请求并等待响应。
    /// </summary>
    Task<TResponse?> RequestAsync<TRequest, TResponse>(TRequest request, int timeoutMilliseconds = 5000);
}

/// <summary>
/// 连接状态枚举。
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Disconnecting,
    Faulted
}

/// <summary>
/// 连接状态变化事件参数。
/// </summary>
public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 变化前状态。
    /// </summary>
    public ConnectionState PreviousState { get; }

    /// <summary>
    /// 变化后状态。
    /// </summary>
    public ConnectionState NewState { get; }

    /// <summary>
    /// 触发错误状态时附带的异常。
    /// </summary>
    public Exception? Error { get; }

    public ConnectionStateChangedEventArgs(
        ConnectionState previous,
        ConnectionState current,
        Exception? error = null)
    {
        PreviousState = previous;
        NewState = current;
        Error = error;
    }
}
