using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 前台控件配置基类。
/// </summary>
public class FrontedControlConfigBase
{
    /// <summary>
    /// 前台行为系统内部使用的控件标识符。
    /// 普通用户不应编辑此值，普通 PropertyGrid 也不应显示此字段。
    /// 复制/粘贴控件时会重新生成，重命名控件时保持不变。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid BehaviorGuid { get; set; }

    /// <summary>
    /// 控件类型。
    /// </summary>
    public string ControlType { get; set; } = string.Empty;

    /// <summary>
    /// Canvas 左侧坐标。
    /// </summary>
    public double Left { get; set; }

    /// <summary>
    /// Canvas 顶部坐标。
    /// </summary>
    public double Top { get; set; }

    /// <summary>
    /// 控件宽度。
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// 控件高度。
    /// </summary>
    public double? Height { get; set; }

    /// <summary>
    /// Canvas 层级。
    /// </summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// 控件可见性。
    /// </summary>
    public FrontedControlVisibility Visibility { get; set; } = FrontedControlVisibility.Visible;

    /// <summary>
    /// 绑定到共享数据服务的属性路径。
    /// </summary>
    public string? BindingPath { get; set; }

    /// <summary>
    /// 行为系统用于触发器过滤的自定义标签字典。
    /// 键为标签名称，值为标签值。在 TriggerFilter 中可通过 SelfTag.X 引用。
    /// Designer UI 编辑入口将在后续版本中提供。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Dictionary<string, string> BehaviorTags { get; set; } = [];
}
