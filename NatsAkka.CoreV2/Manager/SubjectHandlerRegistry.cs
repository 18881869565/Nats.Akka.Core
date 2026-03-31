using NATS.Client;

namespace Nats.Akka.CoreV2.Manager;

/// <summary>
/// 某个主题下的处理器注册表。
/// 通过快照方式遍历，避免边遍历边修改导致并发问题。
/// </summary>
public sealed class SubjectHandlerRegistry
{
    private readonly object _syncRoot = new();
    private readonly List<Func<Msg, CancellationToken, ValueTask>> _handlers = new();

    /// <summary>
    /// 添加一个主题处理器。
    /// </summary>
    public void Add(Func<Msg, CancellationToken, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_syncRoot)
        {
            _handlers.Add(handler);
        }
    }

    /// <summary>
    /// 获取当前处理器快照，供消费线程顺序调用。
    /// </summary>
    public IReadOnlyList<Func<Msg, CancellationToken, ValueTask>> Snapshot()
    {
        lock (_syncRoot)
        {
            return _handlers.ToArray();
        }
    }
}
