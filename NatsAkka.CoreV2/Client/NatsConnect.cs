using Microsoft.Extensions.Configuration;
using Nats.Akka.CoreV2.Extension;
using NATS.Client;

namespace Nats.Akka.CoreV2.Client;

/// <summary>
/// 基于配置创建并维护 NATS 连接的默认实现。
/// </summary>
public sealed class NatsConnect : IConnect
{
    private readonly IConfiguration _configuration;
    private readonly Options _options;
    private IConnection? _connection;
    private ConnectionState _currentState = ConnectionState.Disconnected;

    /// <summary>
    /// 从配置中读取 NATS 连接参数。
    /// 需要至少提供 Nats:Url 与 Nats:DeviceName。
    /// </summary>
    public NatsConnect(IConfiguration configuration)
    {
        _configuration = configuration;
        _options = ConnectionFactory.GetDefaultOptions();
        _options.Url = _configuration["Nats:Url"];
        _options.Name = _configuration["Nats:DeviceName"];
        _options.Timeout = 5000;
        _options.PingInterval = 1000;
        _options.MaxPingsOut = 5;
        _options.AllowReconnect = true;
        _options.MaxReconnect = Options.ReconnectForever;
        // 连接恢复后回到可用状态。
        _options.ReconnectedEventHandler += (_, _) => UpdateState(ConnectionState.Connected);
        // 断线但尚未彻底关闭时，进入重连状态。
        _options.DisconnectedEventHandler += (_, _) => UpdateState(ConnectionState.Reconnecting);
        // 连接关闭后回到断开状态。
        _options.ClosedEventHandler += (_, _) => UpdateState(ConnectionState.Disconnected);
    }

    public ConnectionState CurrentState => _currentState;

    public bool IsConnected => _currentState == ConnectionState.Connected;

    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // 已经连接时不重复创建连接对象。
        if (_connection != null && !_connection.IsClosed())
        {
            await UpdateStateAsync(ConnectionState.Connected);
            return;
        }

        try
        {
            await UpdateStateAsync(ConnectionState.Connecting);
            // 使用线程池执行阻塞式建连，避免调用线程被长时间占用。
            _connection = await Task.Run(() => new ConnectionFactory().CreateConnection(_options), ct);
            await UpdateStateAsync(ConnectionState.Connected);
        }
        catch (Exception ex)
        {
            _connection = null;
            await UpdateStateAsync(ConnectionState.Faulted, ex);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection == null)
        {
            return;
        }

        try
        {
            await UpdateStateAsync(ConnectionState.Disconnecting);
            // Drain 先停止接收新消息并尽量发完缓冲，再关闭连接。
            _connection.Drain();
            _connection.Close();
        }
        finally
        {
            _connection.Dispose();
            _connection = null;
            await UpdateStateAsync(ConnectionState.Disconnected);
        }
    }

    public IConnection GetConnection() =>
        _connection ?? throw new InvalidOperationException("NATS connection has not been established.");

    public void Publish<T>(T message) => GetConnection().Publish(message);

    public TResponse? Request<TRequest, TResponse>(TRequest request, int timeoutMilliseconds = 5000) =>
        GetConnection().Request<TRequest, TResponse>(request, timeoutMilliseconds);

    public Task<TResponse?> RequestAsync<TRequest, TResponse>(TRequest request, int timeoutMilliseconds = 5000) =>
        GetConnection().RequestAsync<TRequest, TResponse>(request, timeoutMilliseconds);

    private void UpdateState(ConnectionState newState, Exception? error = null)
    {
        var previous = _currentState;
        _currentState = newState;
        // 统一从这里触发状态事件，避免各处分散更新状态。
        StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(previous, newState, error));
    }

    private Task UpdateStateAsync(ConnectionState newState, Exception? error = null)
    {
        UpdateState(newState, error);
        return Task.CompletedTask;
    }
}
