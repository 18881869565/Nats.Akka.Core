using K4os.Compression.LZ4;
using System.Text.Json;

namespace Nats.Akka.CoreV2.Internal;

/// <summary>
/// 统一处理消息的 JSON 序列化与 LZ4 压缩。
/// </summary>
internal static class NatsMessageCodec
{
    public static byte[] Serialize<T>(T value)
    {
        // 先转 UTF8 JSON，再做 LZ4 压缩，保持 V2 压测与压缩协议一致。
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        return LZ4Pickler.Pickle(payload);
    }

    public static T Deserialize<T>(byte[] data)
    {
        // 先解压后按 UTF8 JSON 反序列化。
        var decompressed = LZ4Pickler.Unpickle(data);
        var result = JsonSerializer.Deserialize<T>(new ReadOnlySpan<byte>(decompressed));
        return result ?? throw new InvalidOperationException($"Unable to deserialize payload to {typeof(T).FullName}.");
    }

    public static object Deserialize(byte[] data, Type targetType)
    {
        var decompressed = LZ4Pickler.Unpickle(data);
        return JsonSerializer.Deserialize(new ReadOnlySpan<byte>(decompressed), targetType)
            ?? throw new InvalidOperationException($"Unable to deserialize payload to {targetType.FullName}.");
    }
}
