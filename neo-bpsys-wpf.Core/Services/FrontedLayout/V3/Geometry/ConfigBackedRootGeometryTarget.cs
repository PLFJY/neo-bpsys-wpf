using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;

/// <summary>
/// 基于 <see cref="FrontedControlConfigBase"/> 的根控件几何操作目标，
/// 将 Move/Resize 写入 Config 的根级字段并通过回调同步视觉。
/// </summary>
/// <remarks>
/// <para>
/// 该实现是 <see cref="RootControlGeometryTarget"/> 的非 WPF 依赖版本，
/// 供 <see cref="FrontedV3DesignSelectionBuilder"/> 在无 <see cref="FrontedV3ControlHost"/> 的场景下
/// （例如单元测试或 Designer ViewModel 未绑定 Host 时）创建 <see cref="IFrontedV3GeometryTarget"/>。
/// </para>
/// <para>
/// Designer 实际运行时优先使用 <see cref="RootControlGeometryTarget"/>（绑定 Host，直接同步视觉）；
/// 该类型主要服务于 Phase 6 的统一 selection 构建与测试覆盖。
/// </para>
/// <para>
/// 该实现只修改根级几何字段 <c>Left</c>/<c>Top</c>/<c>Width</c>/<c>Height</c>，
/// 不修改 <see cref="FrontedControlConfigBase.ControlType"/>、<c>BehaviorGuid</c>、
/// <c>ZIndex</c>、<c>Visibility</c>、<c>GaussianBlur</c> 等非几何根级字段。
/// </para>
/// </remarks>
internal sealed class ConfigBackedRootGeometryTarget : IFrontedV3GeometryTarget
{
    private readonly FrontedControlConfigBase _config;
    private readonly Action? _onVisualSync;

    /// <summary>
    /// 初始化 <see cref="ConfigBackedRootGeometryTarget"/>。
    /// </summary>
    /// <param name="config">控件配置实例，作为根级字段的单一事实来源。</param>
    /// <param name="onVisualSync">可选的视觉同步回调，在几何值变更后由调用方触发视觉更新。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="config"/> 为 <see langword="null"/> 时抛出。</exception>
    public ConfigBackedRootGeometryTarget(FrontedControlConfigBase config, Action? onVisualSync = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _onVisualSync = onVisualSync;
    }

    /// <inheritdoc />
    public double Left => _config.Left;

    /// <inheritdoc />
    public double Top => _config.Top;

    /// <inheritdoc />
    public double? Width => _config.Width;

    /// <inheritdoc />
    public double? Height => _config.Height;

    /// <inheritdoc />
    public void MoveTo(double left, double top)
    {
        _config.Left = left;
        _config.Top = top;
        _onVisualSync?.Invoke();
    }

    /// <inheritdoc />
    public void ResizeTo(double left, double top, double? width, double? height)
    {
        _config.Left = left;
        _config.Top = top;
        _config.Width = width;
        _config.Height = height;
        _onVisualSync?.Invoke();
    }

    /// <inheritdoc />
    public void ApplyToVisual()
    {
        _onVisualSync?.Invoke();
    }
}
