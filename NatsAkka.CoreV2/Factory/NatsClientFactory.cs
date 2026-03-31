using NATS.Client;

namespace Nats.Akka.CoreV2.Factory;

/// <summary>
/// NATS 连接工厂，负责按指定参数创建底层连接。
/// </summary>
public sealed class NatsClientFactory
{
    /// <summary>
    /// 根据服务地址和客户端名称创建连接。
    /// </summary>
    public IConnection CreateClient(string serverUrl, string clientName, bool reconnectOnConnect = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var factory = new ConnectionFactory();
        var options = ConnectionFactory.GetDefaultOptions();
        options.Url = serverUrl;
        options.Name = clientName;
        return factory.CreateConnection(options, reconnectOnConnect);
    }

    /// <summary>
    /// 根据外部构造好的 Options 创建连接。
    /// </summary>
    public IConnection CreateClient(Options options, bool reconnectOnConnect = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new ConnectionFactory().CreateConnection(options, reconnectOnConnect);
    }
}
