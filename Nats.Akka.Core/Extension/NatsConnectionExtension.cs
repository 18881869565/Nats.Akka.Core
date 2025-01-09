using NATS.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nats.Akka.Core.Extension
{
    public static class NatsConnectionExtension
    {
        public static void Publish<T>(this IConnection connection, T t)
        {
            var type = t.GetType();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(t);
            connection.Publish(type.FullName, bytes);
        }

        public static TRespon? Request<TReq, TRespon>(this IConnection connection, TReq t, int timeoutmilliseconds = 5000)
        {
            var type = t.GetType();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(t);
            var msg = connection.Request(type.FullName, bytes, timeoutmilliseconds);
            if (msg == null)
            {
                return default(TRespon);
            }
            var respon = JsonSerializer.Deserialize<TRespon>(msg.Data);
            return respon;
        }
        public static async Task<TRespon?> RequestAsync<TReq, TRespon>(this IConnection connection, TReq t, int timeoutmilliseconds = 5000)
        {
            var type = t.GetType();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(t);
            var msg = await connection.RequestAsync(type.FullName, bytes, timeoutmilliseconds);
            if (msg == null)
            {
                return default(TRespon);
            }
            var respon = JsonSerializer.Deserialize<TRespon>(msg.Data);
            return respon;
        }
    }
}
