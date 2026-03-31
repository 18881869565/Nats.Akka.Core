namespace Nats.Akka.CoreV2.Manager;

/// <summary>
/// 为计数器操作提供线程安全辅助方法。
/// </summary>
internal static class InterlockedExtensions
{
    public static long DecrementClampToZero(ref long value)
    {
        while (true)
        {
            var current = Volatile.Read(ref value);
            if (current <= 0)
            {
                return 0;
            }

            if (Interlocked.CompareExchange(ref value, current - 1, current) == current)
            {
                return current - 1;
            }
        }
    }
}
