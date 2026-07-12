using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 存储一个前台控件的行为和生成的动画部件。
/// </summary>
public sealed class ControlBehaviorSet
{
    /// <summary>
    /// 获取或设置所属前台控件的稳定行为标识符。
    /// </summary>
    public Guid BehaviorGuid { get; set; }

    /// <summary>
    /// 获取或设置所属前台控件的用户可见名称。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置此行为集合拥有的生成动画辅助可视化元素。
    /// </summary>
    public List<FrontedAnimationPartConfig> AnimationParts { get; set; } = [];

    /// <summary>
    /// 获取或设置此前台控件拥有的行为图。
    /// </summary>
    public List<FrontedBehavior> Behaviors { get; set; } = [];
}

