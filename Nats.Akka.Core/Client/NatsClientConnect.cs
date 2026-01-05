using Microsoft.Extensions.Configuration;
using Nats.Akka.Core.Extension;
using NATS.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NatsAkka.Core.Client
{
    public class NatsConnect : IConnect
    {
        private readonly IConfiguration _configuration;
        /// <summary>
        /// 配置
        /// </summary>
        private Options _options;
        private IConnection? _connection;
        /// <summary>
        /// 连接状态
        /// </summary>
        private ConnectionState _currentState = ConnectionState.Disconnected;
        public ConnectionState CurrentState => _currentState;

        public bool IsConnected => _currentState == ConnectionState.Connected;

        public event EventHandler<ConnectionStateChangedEventArgs> StateChanged;

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
            _options.MaxReconnect = NATS.Client.Options.ReconnectForever;
            _options.ReconnectedEventHandler += ReconnectedEventHandler;
            _options.DisconnectedEventHandler += DisconnectedEventHandler;
            _options.ClosedEventHandler += ClosedEventHandler;
        }

        private void ClosedEventHandler(object? sender, ConnEventArgs e)
        {

        }

        private void DisconnectedEventHandler(object? sender, ConnEventArgs e)
        {

        }

        private void ReconnectedEventHandler(object? sender, ConnEventArgs e)
        {

        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            //异步循环判断_connection是否为空，如果为空则创建连接，如果不为空则循环获取_connection的状态，并发送状态变更事件
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_connection == null)
                    {
                        await UpdateState(ConnectionState.Connecting);
                        // 将阻塞操作移至线程池线程
                        _connection = await Task.Run(() => new ConnectionFactory().CreateConnection(_options), ct);
                        await UpdateState(ConnectionState.Connected);
                    }
                    else
                    {
                        if (_connection.IsReconnecting())
                        {
                            await UpdateState(ConnectionState.Reconnecting);

                        }
                        else if (_connection.IsClosed())
                        {
                            await UpdateState(ConnectionState.Disconnected);
                        }
                        else
                        {
                            await UpdateState(ConnectionState.Connected);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await UpdateState(ConnectionState.Faulted);
                    if (_connection == null)
                    {

                    }
                }
                await Task.Delay(1000);
            }

        }

        public async Task DisconnectAsync()
        {
            // 避免重复断开
            if (_connection == null || _connection.IsClosed())
                return;

            try
            {
                await UpdateState(ConnectionState.Disconnecting);

                // 1. Drain: 停止接收新消息，但发送完缓冲区中的消息
                _connection.Drain();

                // 2. Close: 关闭连接（Drain() 后通常已自动关闭，但显式调用更安全）
                _connection.Close();
            }
            catch (Exception ex)
            {
                // 可选：记录日志
                // _logger?.LogWarning(ex, "Error during NATS disconnect");
            }
            finally
            {
                // 3. 释放资源
                _connection.Dispose();
                _connection = null;

                // 4. 更新最终状态
                await UpdateState(ConnectionState.Disconnected);
            }
        }
        private Task UpdateState(ConnectionState newState, Exception? error = null)
        {
            var previous = _currentState;

            _currentState = newState;

            StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(previous, newState, error));

            return Task.CompletedTask; // 不再用 Task.Run
        }

        public IConnection GetConnection()
        {
            return _connection;
        }

        public void Publish<T>(T message)
        {
            GetConnection().Publish(message);
        }

        public TResponse? Request<TRequest, TResponse>(TRequest request, int timeoutMilliseconds = 5000)
        {
            return GetConnection().Request<TRequest, TResponse>(request, timeoutMilliseconds);
        }

        public async Task<TResponse?> RequestAsync<TRequest, TResponse>(TRequest request, int timeoutMilliseconds = 5000)
        {
            return await GetConnection().RequestAsync<TRequest, TResponse>(request, timeoutMilliseconds);
        }
    }
}
