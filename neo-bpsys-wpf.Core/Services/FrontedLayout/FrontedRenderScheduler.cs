using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台渲染更新调度器。
/// </summary>
/// <remarks>
/// <para>
/// 共享数据的属性变化可能在同一帧内连续触发多次（例如颜色选择器拖动、连续比分更新）。
/// 本调度器以调用方提供的 owner 为键合并待执行动作，把同一 owner 的多次请求折叠为
/// 一次 Dispatcher 队列回调，从而避免重复重建画刷、位图或文本布局。
/// </para>
/// <para>
/// 该类型只负责“何时执行”，不负责“如何计算”；昂贵且可离屏的产物应由调用方自行放到后台线程，
/// 并在结果冻结后回到 UI 线程应用。
/// </para>
/// </remarks>
internal static class FrontedRenderScheduler
{
    private static readonly object Gate = new();
    private static readonly Dictionary<object, Action> PendingActions = new(ReferenceEqualityComparer.Instance);
    private static bool _flushScheduled;

    /// <summary>
    /// 以 <paramref name="owner"/> 为键合并调度一个 UI 线程动作。
    /// </summary>
    /// <param name="owner">合并键；同一对象的多次调度只保留最后一次动作。</param>
    /// <param name="action">在 Dispatcher 渲染优先级执行的动作。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="owner"/> 或 <paramref name="action"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <remarks>
    /// 没有应用 Dispatcher（例如纯单元测试或 Designer 同步预览）时直接执行动作，
    /// 保证调用方不依赖 WPF 消息循环也能得到结果。
    /// </remarks>
    public static void Schedule(object owner, Action action)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            action();
            return;
        }

        lock (Gate)
        {
            PendingActions[owner] = action;
            if (_flushScheduled)
            {
                return;
            }

            _flushScheduled = true;
        }

        try
        {
            dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(Flush));
        }
        catch (InvalidOperationException)
        {
            // Dispatcher 正在关闭：退化为同步执行，避免动作被静默丢弃。
            lock (Gate)
            {
                _flushScheduled = false;
                PendingActions.Remove(owner);
            }

            action();
        }
    }

    /// <summary>
    /// 取消指定 owner 尚未执行的合并动作。
    /// </summary>
    /// <param name="owner">调度合并键。</param>
    public static void Cancel(object owner)
    {
        lock (Gate)
        {
            PendingActions.Remove(owner);
        }
    }

    private static void Flush()
    {
        Action[] actions;
        lock (Gate)
        {
            _flushScheduled = false;
            actions = PendingActions.Values.ToArray();
            PendingActions.Clear();
        }

        foreach (var action in actions)
        {
            action();
        }
    }
}
