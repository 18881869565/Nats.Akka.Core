using NATS.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NatsAkka.Core.Client
{
    public interface IConnect
    {
        ConnectionState CurrentState { get; }
        bool IsConnected { get; }
        Task ConnectAsync(CancellationToken ct = default);
        Task DisconnectAsync();
        event EventHandler<ConnectionStateChangedEventArgs> StateChanged;
        IConnection GetConnection(); 
        void Publish<T>(T message);
        TResponse? Request<TRequest, TResponse>(TRequest request, int timeoutMilliseconds = 5000);
        Task<TResponse?> RequestAsync<TRequest, TResponse>(TRequest request, int timeoutMilliseconds = 5000);
    }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Disconnecting,
        Faulted
    }

    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public ConnectionState PreviousState { get; }
        public ConnectionState NewState { get; }
        public Exception Error { get; }

        public ConnectionStateChangedEventArgs(
            ConnectionState previous,
            ConnectionState current,
            Exception error = null)
        {
            PreviousState = previous;
            NewState = current;
            Error = error;
        }
    }
}
