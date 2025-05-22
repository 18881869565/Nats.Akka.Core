using K4os.Compression.LZ4;
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
            byte[] compressedJson = LZ4Pickler.Pickle(bytes);
            connection.Publish(type.FullName, compressedJson);
        }

        public static TRespon? Request<TReq, TRespon>(this IConnection connection, TReq t, int timeoutmilliseconds = 5000)
        {
            var type = t.GetType();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(t);
            byte[] compressedJson = LZ4Pickler.Pickle(bytes);
            var msg = connection.Request(type.FullName, compressedJson, timeoutmilliseconds);
            if (msg == null)
            {
                return default(TRespon);
            }
            var decompressed = LZ4Pickler.Unpickle(msg.Data);
            var respon = JsonSerializer.Deserialize<TRespon>(decompressed);
            return respon;
        }
        public static async Task<TRespon?> RequestAsync<TReq, TRespon>(this IConnection connection, TReq t, int timeoutmilliseconds = 5000)
        {
            var type = t.GetType();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(t);
            byte[] compressedJson = LZ4Pickler.Pickle(bytes);
            var msg = await connection.RequestAsync(type.FullName, compressedJson, timeoutmilliseconds);
            if (msg == null)
            {
                return default(TRespon);
            }
            var decompressed = LZ4Pickler.Unpickle(msg.Data);
            var respon = JsonSerializer.Deserialize<TRespon>(decompressed);
            return respon;
        }
    }
}
