using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台冻结纯色画刷缓存。
/// </summary>
/// <remarks>
/// 颜色绑定（形状填充、文本前景色等）可能在同一次交互中反复解析相同颜色。
/// 本缓存让相同颜色复用同一个已冻结的 <see cref="SolidColorBrush"/>，减少绑定转换器的分配与
/// WPF 属性系统变更；只缓存纯色，不缓存渐变或动画中的中间画刷。
/// </remarks>
internal static class FrontedBrushCache
{
    private const int MaxEntries = 128;
    private static readonly object Gate = new();
    private static readonly Dictionary<Color, SolidColorBrush> Cache = [];
    private static readonly Dictionary<Color, LinkedListNode<Color>> Nodes = [];
    private static readonly LinkedList<Color> Order = [];

    /// <summary>
    /// 获取指定颜色的冻结纯色画刷。
    /// </summary>
    /// <param name="color">颜色。</param>
    /// <returns>已冻结、可在多线程与多个元素间共享的画刷。</returns>
    public static SolidColorBrush GetSolid(Color color)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(color, out var brush) && Nodes.TryGetValue(color, out var cachedNode))
            {
                Order.Remove(cachedNode);
                Order.AddLast(cachedNode);
                return brush;
            }

            brush = new SolidColorBrush(color);
            brush.Freeze();
            var node = Order.AddLast(color);
            Nodes[color] = node;
            Cache[color] = brush;

            while (Cache.Count > MaxEntries && Order.First is { } oldest)
            {
                var oldestColor = oldest.Value;
                Order.RemoveFirst();
                Cache.Remove(oldestColor);
                Nodes.Remove(oldestColor);
            }

            return brush;
        }
    }
}
