using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;

/// <summary>
/// 根控件几何操作目标，将 Designer 的 Move/Resize 写入同步到 Config 与 Host 视觉。
/// </summary>
/// <remarks>
/// <para>
/// 该实现是 Phase 6 Designer 去特化的基础：所有根级 Move/Resize/Snap/Clamp/Undo 都通过
/// <see cref="IFrontedV3GeometryTarget"/> 调用，不再通过 <c>if (config is BorderedImage...)</c>
/// 等类型分支选择几何实现。
/// </para>
/// <para>
/// 该实现只修改根级几何字段 <c>Left</c>/<c>Top</c>/<c>Width</c>/<c>Height</c>，
/// 不修改 <see cref="FrontedControlConfigBase.ControlType"/>、<c>BehaviorGuid</c>、
/// <c>ZIndex</c>、<c>Visibility</c>、<c>GaussianBlur</c> 等非几何根级字段。
/// </para>
/// <para>
/// 写入流程：将几何值写入 <see cref="FrontedControlConfigBase"/> → 调用
/// <see cref="FrontedV3ControlHost.ApplyRootLayout"/> 同步视觉。
/// </para>
/// </remarks>
internal sealed class RootControlGeometryTarget : IFrontedV3GeometryTarget
{
    private readonly FrontedV3ControlHost _host;
    private readonly FrontedControlConfigBase _config;

    /// <summary>
    /// 初始化 <see cref="RootControlGeometryTarget"/> 并绑定到指定的 Host 与 Config。
    /// </summary>
    /// <param name="host">根布局宿主，用于同步视觉。</param>
    /// <param name="config">控件配置实例，作为根级字段的单一事实来源。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="host"/> 或 <paramref name="config"/> 为 <see langword="null"/> 时抛出。</exception>
    public RootControlGeometryTarget(FrontedV3ControlHost host, FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(config);
        _host = host;
        _config = config;
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
        _host.ApplyRootLayout();
    }

    /// <inheritdoc />
    public void ResizeTo(double left, double top, double? width, double? height)
    {
        _config.Left = left;
        _config.Top = top;
        _config.Width = width;
        _config.Height = height;
        _host.ApplyRootLayout();
    }

    /// <inheritdoc />
    public void ApplyToVisual()
    {
        _host.ApplyRootLayout();
    }
}
