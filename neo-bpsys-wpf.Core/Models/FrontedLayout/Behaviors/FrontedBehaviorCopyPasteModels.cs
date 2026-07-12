using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 在控件间复制单个前台行为时使用的应用级剪贴板负载。
/// </summary>
public sealed class FrontedBehaviorClipboardPayload
{
    /// <summary>
    /// 获取或设置剪贴板负载版本。
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 获取或设置源窗口类型。
    /// </summary>
    public string SourceWindowType { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置源控件名称。
    /// </summary>
    public string SourceControlName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置源控件的行为 GUID。
    /// </summary>
    public Guid SourceControlBehaviorGuid { get; set; }

    /// <summary>
    /// 获取或设置源控件的推断语义索引。
    /// </summary>
    public int? SourceSemanticIndex { get; set; }

    /// <summary>
    /// 获取或设置复制的行为快照。
    /// </summary>
    public FrontedBehavior Behavior { get; set; } = new();

    /// <summary>
    /// 获取或设置生成部件和控件类型要求。
    /// </summary>
    public List<FrontedBehaviorCopyRequirement> Requirements { get; set; } = [];

    /// <summary>
    /// 获取或设置所复制行为需要的源动画部件定义。
    /// </summary>
    public List<FrontedAnimationPartConfig> AnimationParts { get; set; } = [];
}

/// <summary>
/// 描述复制行为时发现的一项兼容性要求。
/// </summary>
public sealed class FrontedBehaviorCopyRequirement
{
    /// <summary>
    /// 获取或设置要求类型。
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置导致该要求的目标引用。
    /// </summary>
    public string? Source { get; set; }
}

/// <summary>
/// 粘贴已复制行为时应用的选项。
/// </summary>
public sealed class FrontedBehaviorPasteOptions
{
    /// <summary>
    /// 获取或设置是否重写源控件动画目标。
    /// </summary>
    public bool RewriteAnimationTargets { get; set; } = true;

    /// <summary>
    /// 获取或设置是否重写支持的触发条件索引过滤器。
    /// </summary>
    public bool RewriteTriggerIndexes { get; set; } = true;

    /// <summary>
    /// 获取或设置粘贴的行为是否获得新的行为标识符。
    /// </summary>
    public bool GenerateNewBehaviorId { get; set; } = true;

    /// <summary>
    /// 获取或设置是否保留复制的行为名称。
    /// </summary>
    public bool KeepBehaviorName { get; set; } = true;
}

/// <summary>
/// 从前台控件推断的语义信息。
/// </summary>
public sealed class FrontedBehaviorControlSemanticInfo
{
    /// <summary>
    /// 获取或设置控件特定的语义索引。
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    /// 获取或设置推断的语义角色。
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// 获取或设置推断的语义分组。
    /// </summary>
    public string? Group { get; set; }
}

/// <summary>
/// 解析在控件间复制行为时使用的语义信息。
/// </summary>
public interface IFrontedBehaviorControlSemanticResolver
{
    /// <summary>
    /// 解析设计控件的语义信息。
    /// </summary>
    /// <param name="control">要检查的设计控件。</param>
    /// <returns>推断出的语义信息。</returns>
    FrontedBehaviorControlSemanticInfo Resolve(FrontedControlDesignItem control);
}

/// <summary>
/// 描述行为粘贴预览中显示的一项值重写。
/// </summary>
public sealed class FrontedBehaviorPasteRewrite
{
    /// <summary>
    /// 获取或设置原始值。
    /// </summary>
    public string Before { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置重写后的值。
    /// </summary>
    public string After { get; set; } = string.Empty;
}

/// <summary>
/// 描述单个行为粘贴目标的兼容性和重写。
/// </summary>
public sealed class FrontedBehaviorPastePreview
{
    /// <summary>
    /// 获取或设置目标控件。
    /// </summary>
    public required FrontedControlDesignItem Target { get; set; }

    /// <summary>
    /// 获取或设置目标是否兼容。
    /// </summary>
    public bool IsCompatible { get; set; }

    /// <summary>
    /// 获取或设置兼容性错误。
    /// </summary>
    public List<string> CompatibilityErrors { get; set; } = [];

    /// <summary>
    /// 获取或设置动画目标重写。
    /// </summary>
    public List<FrontedBehaviorPasteRewrite> TargetRewrites { get; set; } = [];

    /// <summary>
    /// 获取或设置触发条件过滤器重写。
    /// </summary>
    public List<FrontedBehaviorPasteRewrite> TriggerRewrites { get; set; } = [];

    /// <summary>
    /// 获取或设置有意保持不变的外部目标引用。
    /// </summary>
    public List<string> ExternalReferences { get; set; } = [];

    /// <summary>
    /// 获取或设置触发条件索引重映射是否可用。
    /// </summary>
    public bool IsTriggerIndexRemapAvailable { get; set; }

    /// <summary>
    /// 获取或设置触发条件重映射不可用的原因。
    /// </summary>
    public string? TriggerIndexRemapUnavailableReason { get; set; }
}

/// <summary>
/// 粘贴一个已复制行为后返回的结果。
/// </summary>
public sealed class FrontedBehaviorPasteResult
{
    /// <summary>
    /// 获取或设置行为是否已粘贴。
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// 获取或设置粘贴预览和兼容性结果。
    /// </summary>
    public required FrontedBehaviorPastePreview Preview { get; set; }

    /// <summary>
    /// 获取或设置成功时粘贴的行为。
    /// </summary>
    public FrontedBehavior? Behavior { get; set; }
}

/// <summary>
/// 存储当前应用级行为剪贴板负载。
/// </summary>
public interface IFrontedBehaviorClipboard
{
    /// <summary>
    /// 获取当前剪贴板负载。
    /// </summary>
    FrontedBehaviorClipboardPayload? Payload { get; }

    /// <summary>
    /// 替换当前剪贴板负载。
    /// </summary>
    /// <param name="payload">要存储的负载。</param>
    void Set(FrontedBehaviorClipboardPayload payload);
}
