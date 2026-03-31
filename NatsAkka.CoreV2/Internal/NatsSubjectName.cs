namespace Nats.Akka.CoreV2.Internal;

/// <summary>
/// 统一生成 NATS 主题名，默认使用类型全名。
/// </summary>
internal static class NatsSubjectName
{
    public static string For<T>() => For(typeof(T));

    public static string For<T>(T message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return For(message.GetType());
    }

    public static string For(Type type) =>
        type.FullName ?? throw new InvalidOperationException($"Type {type.Name} does not have a valid FullName.");
}
