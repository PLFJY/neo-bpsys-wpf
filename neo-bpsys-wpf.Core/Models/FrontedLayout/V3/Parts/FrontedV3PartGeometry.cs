namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

/// <summary>
/// 固定 Part 的几何信息，坐标相对于父 Control（不是 Canvas 绝对坐标）。
/// </summary>
/// <remarks>
/// <para>
/// Part 几何与根控件几何不同：根控件使用 Canvas 绝对坐标（<c>Left</c>/<c>Top</c>），
/// Part 使用相对于父 Control 的坐标（<c>X</c>/<c>Y</c>）。
/// </para>
/// <para>
/// <see cref="Width"/>/<see cref="Height"/> 为 <see langword="null"/> 时表示该维度无显式尺寸
/// （由父 Control 布局槽决定）。
/// </para>
/// </remarks>
public sealed class FrontedV3PartGeometry
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3PartGeometry"/>。
    /// </summary>
    public FrontedV3PartGeometry()
    {
    }

    /// <summary>
    /// 初始化 <see cref="FrontedV3PartGeometry"/> 并指定全部几何值。
    /// </summary>
    /// <param name="x">相对于父 Control 的左侧坐标。</param>
    /// <param name="y">相对于父 Control 的顶部坐标。</param>
    /// <param name="width">显式宽度；为 <see langword="null"/> 时表示无显式尺寸。</param>
    /// <param name="height">显式高度；为 <see langword="null"/> 时表示无显式尺寸。</param>
    public FrontedV3PartGeometry(double x, double y, double? width, double? height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// 获取或设置相对于父 Control 的左侧坐标。
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// 获取或设置相对于父 Control 的顶部坐标。
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// 获取或设置显式宽度；为 <see langword="null"/> 时表示无显式尺寸。
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// 获取或设置显式高度；为 <see langword="null"/> 时表示无显式尺寸。
    /// </summary>
    public double? Height { get; set; }
}
