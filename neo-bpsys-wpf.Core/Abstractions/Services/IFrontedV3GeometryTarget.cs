using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台控件几何操作的抽象接口，由 Designer 的 Move/Resize/Snap/Clamp/Undo 统一调用。
/// </summary>
/// <remarks>
/// <para>
/// 该接口在 Phase 2 引入，用于将根控件的几何写入与视觉更新从 Designer ViewModel 中解耦。
/// Phase 6 完成去特化后，所有 Move/Resize 只调用本接口，不再通过 <c>if (config is BorderedImage...)</c>
/// 等类型分支选择几何实现。
/// </para>
/// <para>
/// 实现负责：
/// <list type="bullet">
/// <item>将几何值写入当前 <see cref="FrontedControlConfigBase"/>（根级字段 <c>Left</c>/<c>Top</c>/<c>Width</c>/<c>Height</c>）。</item>
/// <item>调用宿主 <c>FrontedV3ControlHost.ApplyRootLayout</c> 同步视觉。</item>
/// </list>
/// </para>
/// <para>
/// 实现不得修改 <see cref="FrontedControlConfigBase.ControlType"/>、<c>BehaviorGuid</c>、<c>ZIndex</c>、
/// <c>Visibility</c>、<c>GaussianBlur</c> 等非几何根级字段。
/// </para>
/// </remarks>
public interface IFrontedV3GeometryTarget
{
    /// <summary>
    /// 获取当前根控件左侧坐标。
    /// </summary>
    double Left { get; }

    /// <summary>
    /// 获取当前根控件顶部坐标。
    /// </summary>
    double Top { get; }

    /// <summary>
    /// 获取当前根控件宽度；为 <see langword="null"/> 时表示控件使用自适应尺寸。
    /// </summary>
    double? Width { get; }

    /// <summary>
    /// 获取当前根控件高度；为 <see langword="null"/> 时表示控件使用自适应尺寸。
    /// </summary>
    double? Height { get; }

    /// <summary>
    /// 将根控件移动到指定坐标，并同步视觉。
    /// </summary>
    /// <param name="left">目标左侧坐标。</param>
    /// <param name="top">目标顶部坐标。</param>
    void MoveTo(double left, double top);

    /// <summary>
    /// 将根控件调整为指定矩形，并同步视觉。
    /// </summary>
    /// <param name="left">目标左侧坐标。</param>
    /// <param name="top">目标顶部坐标。</param>
    /// <param name="width">目标宽度；为 <see langword="null"/> 时清除显式宽度（恢复自适应）。</param>
    /// <param name="height">目标高度；为 <see langword="null"/> 时清除显式高度（恢复自适应）。</param>
    void ResizeTo(double left, double top, double? width, double? height);

    /// <summary>
    /// 将当前 Config 中的几何值重新应用到视觉宿主，用于 Config 被外部修改后恢复一致性。
    /// </summary>
    void ApplyToVisual();
}
