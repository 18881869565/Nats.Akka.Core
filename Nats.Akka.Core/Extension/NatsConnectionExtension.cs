using NATS.Client;
using Nats.Akka.Core.Internal;
using System;
using System.Threading.Tasks;

namespace Nats.Akka.Core.Extension
{
    public static class NatsConnectionExtension
    {
        public static void Publish<T>(this IConnection connection, T t)
        {
            var subject = GetSubjectName(t);
            connection.Publish(subject, NatsMessageCodec.Serialize(t));
        }

        public static void Publish<T>(this IConnection connection, string subject, T t)
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new ArgumentException("Subject cannot be null or whitespace.", nameof(subject));
            }

            connection.Publish(subject, NatsMessageCodec.Serialize(t));
        }

        public static TRespon? Request<TReq, TRespon>(this IConnection connection, TReq t, int timeoutmilliseconds = 5000)
        {
            var subject = GetSubjectName(t);
            var msg = connection.Request(subject, NatsMessageCodec.Serialize(t), timeoutmilliseconds);
            if (msg == null)
            {
                return default(TRespon);
            }

            return NatsMessageCodec.Deserialize<TRespon>(msg.Data);
        }

        public static async Task<TRespon?> RequestAsync<TReq, TRespon>(this IConnection connection, TReq t, int timeoutmilliseconds = 5000)
        {
            var subject = GetSubjectName(t);
            var msg = await connection.RequestAsync(subject, NatsMessageCodec.Serialize(t), timeoutmilliseconds);
            if (msg == null)
            {
                return default(TRespon);
            }

            return NatsMessageCodec.Deserialize<TRespon>(msg.Data);
        }

        private static string GetSubjectName<T>(T value)
        {
            var type = value?.GetType() ?? typeof(T);
            return type.FullName ?? throw new InvalidOperationException($"Type {type.Name} does not have a valid FullName.");
        }
    }
}
