using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using System.Windows;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 描述接收动画动作的可视化层。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedAnimationTargetLayer
{
    /// <summary>
    /// 根据属性名和目标控件形状自动选择目标层。
    /// </summary>
    Auto,

    /// <summary>
    /// 将动作应用于生成的控件根元素。
    /// </summary>
    Control,

    /// <summary>
    /// 将动作应用于目标控件的主要内容元素。
    /// </summary>
    Content,

    /// <summary>
    /// 将动作应用于目标控件上方的运行时矩形覆盖层。
    /// </summary>
    OverlayAbove,

    /// <summary>
    /// 将动作应用于目标控件下方的运行时矩形覆盖层。
    /// </summary>
    OverlayBelow
}

/// <summary>
/// 标识持久化动画目标引用的类型。
/// </summary>
public enum FrontedAnimationTargetReferenceKind
{
    /// <summary>
    /// 拥有正在执行的行为的控件。
    /// </summary>
    Self,

    /// <summary>
    /// 由行为 GUID 标识的生成控件。
    /// </summary>
    BehaviorGuid,

    /// <summary>
    /// 由注册名称标识的生成控件。
    /// </summary>
    RegisteredName,

    /// <summary>
    /// 由所属行为 GUID 和稳定部件名标识的生成辅助部件。
    /// </summary>
    GeneratedPart
}

/// <summary>
/// 从行为图动作节点解析的持久化动画目标引用。
/// </summary>
public sealed class FrontedAnimationTargetReference
{
    /// <summary>
    /// 获取目标引用类型。
    /// </summary>
    public FrontedAnimationTargetReferenceKind Kind { get; init; } = FrontedAnimationTargetReferenceKind.Self;

    /// <summary>
    /// 当 <see cref="Kind" /> 为 <see cref="FrontedAnimationTargetReferenceKind.BehaviorGuid" /> 时获取行为 GUID。
    /// </summary>
    public Guid? BehaviorGuid { get; init; }

    /// <summary>
    /// 当 <see cref="Kind" /> 为 <see cref="FrontedAnimationTargetReferenceKind.RegisteredName" /> 时获取注册元素名称。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 当 <see cref="Kind" /> 为 <see cref="FrontedAnimationTargetReferenceKind.GeneratedPart" /> 时获取稳定的生成部件名称。
    /// </summary>
    public string? PartName { get; init; }

    /// <summary>
    /// 获取用户可见的显示名称（若可用）。
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 解析存储的目标引用字符串。
    /// </summary>
    /// <param name="value">存储的目标引用。</param>
    /// <returns>解析后的目标引用。</returns>
    public static FrontedAnimationTargetReference Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value.Trim(), "Self", StringComparison.OrdinalIgnoreCase))
        {
            return new FrontedAnimationTargetReference { Kind = FrontedAnimationTargetReferenceKind.Self };
        }

        var text = value.Trim();
        if (text.StartsWith("part:", StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = text.IndexOf(':', "part:".Length);
            if (separatorIndex > "part:".Length
                && Guid.TryParse(text["part:".Length..separatorIndex].Trim('{', '}', ' '), out var partGuid))
            {
                var partName = text[(separatorIndex + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(partName))
                {
                    return new FrontedAnimationTargetReference
                    {
                        Kind = FrontedAnimationTargetReferenceKind.GeneratedPart,
                        BehaviorGuid = partGuid,
                        PartName = partName,
                        DisplayName = value
                    };
                }
            }
        }

        if (text.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["name:".Length..].Trim();
            return new FrontedAnimationTargetReference
            {
                Kind = FrontedAnimationTargetReferenceKind.RegisteredName,
                Name = text,
                DisplayName = value
            };
        }

        if (text.StartsWith("guid:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["guid:".Length..].Trim();
        }

        text = text.Trim('{', '}');
        if (Guid.TryParse(text, out var guid))
        {
            return new FrontedAnimationTargetReference
            {
                Kind = FrontedAnimationTargetReferenceKind.BehaviorGuid,
                BehaviorGuid = guid,
                DisplayName = value
            };
        }

        return new FrontedAnimationTargetReference
        {
            Kind = FrontedAnimationTargetReferenceKind.RegisteredName,
            Name = value,
            DisplayName = value
        };
    }
}

/// <summary>
/// 解析持久化目标引用和可视化层后的运行时动画目标。
/// </summary>
public sealed class FrontedAnimationTarget
{
    /// <summary>
    /// 获取接收动画动作的 WPF 元素。
    /// </summary>
    public required FrameworkElement Element { get; init; }

    /// <summary>
    /// 获取所属生成控件的行为 GUID。
    /// </summary>
    public Guid BehaviorGuid { get; init; }

    /// <summary>
    /// 获取解析后的目标名称。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 获取用户可见的显示名称（若可用）。
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 获取解析后的可视化目标层。
    /// </summary>
    public FrontedAnimationTargetLayer TargetLayer { get; init; } = FrontedAnimationTargetLayer.Control;

    /// <summary>
    /// 获取层解析前使用的生成控件根元素。
    /// </summary>
    public FrameworkElement? ControlElement { get; init; }
}

/// <summary>
/// 应用 WPF 动画动作时使用的运行时上下文。
/// </summary>
public sealed class FrontedAnimationExecutionContext
{
    /// <summary>
    /// 获取限定目标查找和运行时会话的根元素。
    /// </summary>
    public required FrameworkElement Root { get; init; }

    /// <summary>
    /// 获取拥有正在执行行为的控件的行为 GUID。
    /// </summary>
    public Guid SelfBehaviorGuid { get; init; }

    /// <summary>
    /// 获取拥有正在执行行为的控件的用户可见名称。
    /// </summary>
    public string? SelfDisplayName { get; init; }

    /// <summary>
    /// 获取当前前台窗口标识符。
    /// </summary>
    public string WindowId { get; init; } = string.Empty;

    /// <summary>
    /// 获取当前画布名称。
    /// </summary>
    public string CanvasName { get; init; } = string.Empty;

    /// <summary>
    /// 获取一个值，指示动作是否在设计器预览中运行。
    /// </summary>
    public bool IsDesignerPreview { get; init; }

    /// <summary>
    /// 获取用于运行时警告的可选日志记录器。
    /// </summary>
    public ILogger? Logger { get; init; }

    /// <summary>
    /// 获取当前动画动作的取消令牌。
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// 创建使用替换取消令牌的上下文副本。
    /// </summary>
    /// <param name="cancellationToken">替换的取消令牌。</param>
    /// <returns>使用替换令牌的上下文副本。</returns>
    public FrontedAnimationExecutionContext WithCancellationToken(CancellationToken cancellationToken) =>
        new()
        {
            Root = Root,
            SelfBehaviorGuid = SelfBehaviorGuid,
            SelfDisplayName = SelfDisplayName,
            WindowId = WindowId,
            CanvasName = CanvasName,
            IsDesignerPreview = IsDesignerPreview,
            Logger = Logger,
            CancellationToken = cancellationToken
        };
}
