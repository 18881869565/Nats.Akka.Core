using K4os.Compression.LZ4;
using System.Text.Json;

namespace Nats.Akka.Core.Internal
{
    internal static class NatsMessageCodec
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            IncludeFields = true
        };

        public static byte[] Serialize<T>(T value)
        {
            var payload = value is null
                ? JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions)
                : JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), JsonOptions);

            return LZ4Pickler.Pickle(payload);
        }

        public static T? Deserialize<T>(byte[] data)
        {
            var decompressed = LZ4Pickler.Unpickle(data);
            return JsonSerializer.Deserialize<T>(new ReadOnlySpan<byte>(decompressed), JsonOptions);
        }

        public static object? Deserialize(byte[] data, Type targetType)
        {
            var decompressed = LZ4Pickler.Unpickle(data);
            return JsonSerializer.Deserialize(new ReadOnlySpan<byte>(decompressed), targetType, JsonOptions);
        }
    }
}
