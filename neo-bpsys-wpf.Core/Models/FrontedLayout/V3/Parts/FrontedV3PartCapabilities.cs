namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

/// <summary>
/// 固定 Part 的操作能力，决定 Designer 中允许对该 Part 执行的几何操作类型。
/// </summary>
/// <remarks>
/// <para>
/// 能力是显式约束：<see cref="CanMove"/> 为 <see langword="false"/> 时 Designer 不得调用
/// <see cref="Abstractions.Services.IFrontedV3GeometryTarget.MoveTo"/>；
/// <see cref="CanResize"/> 为 <see langword="false"/> 时 Designer 不得调用
/// <see cref="Abstractions.Services.IFrontedV3GeometryTarget.ResizeTo"/>。
/// </para>
/// <para>
/// 典型用法：
/// <list type="bullet">
/// <item>BorderedImage 内层 Image：<see cref="Resize"/>（只允许 Resize，不允许 Move）。</item>
/// <item>MapV2 内部部件：<see cref="MoveAndResize"/>（允许 Move 与 Resize）。</item>
/// <item>只读装饰部件：<see cref="None"/>（不允许任何操作）。</item>
/// </list>
/// </para>
/// </remarks>
public sealed class FrontedV3PartCapabilities
{
    /// <summary>
    /// 获取该 Part 是否允许移动。
    /// </summary>
    public bool CanMove { get; init; }

    /// <summary>
    /// 获取该 Part 是否允许缩放。
    /// </summary>
    public bool CanResize { get; init; }

    /// <summary>
    /// 不允许任何几何操作。
    /// </summary>
    public static FrontedV3PartCapabilities None { get; } = new();

    /// <summary>
    /// 仅允许移动。
    /// </summary>
    public static FrontedV3PartCapabilities Move { get; } = new() { CanMove = true };

    /// <summary>
    /// 仅允许缩放。
    /// </summary>
    public static FrontedV3PartCapabilities Resize { get; } = new() { CanResize = true };

    /// <summary>
    /// 同时允许移动与缩放。
    /// </summary>
    public static FrontedV3PartCapabilities MoveAndResize { get; } = new() { CanMove = true, CanResize = true };
}
