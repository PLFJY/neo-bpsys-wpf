namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// GameData 识别区域配置模型（当前实现）。
/// 这是业务层配置结构，后续可由更通用的 RegionLayoutDefinition 适配得到。
/// </summary>
public sealed class SmartBpRegionProfile
{
    /// <summary>
    /// 配置版本号，用于未来结构升级时做兼容迁移。
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 场景名，当前固定为 GameData。
    /// </summary>
    public string Scene { get; set; } = "GameData";

    /// <summary>
    /// 这份配置期望使用的纵横比（如 16:9）。
    /// 只用于提示，不硬性阻止识别。
    /// </summary>
    public string BaseAspectRatio { get; set; } = "16:9";

    /// <summary>
    /// 生成/编辑该配置时的基准捕获尺寸，仅用于诊断与显示。
    /// </summary>
    public WindowSize BaseSize { get; set; } = new(1920, 1080);

    /// <summary>
    /// 行级结构（当前约定 5 行：1 监管者 + 4 求生者）。
    /// </summary>
    public List<SmartBpRegionRow> Rows { get; set; } = [];
}

/// <summary>
/// 一行识别区域（大框 + 若干小框）。
/// </summary>
public sealed class SmartBpRegionRow
{
    /// <summary>
    /// 行标识（持久化 ID，便于后续做结构迁移时定位）。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 行大框，相对于整帧。
    /// </summary>
    public RelativeRect BigRect { get; set; }

    /// <summary>
    /// 行内小框集合，小框坐标相对于 BigRect。
    /// </summary>
    public List<SmartBpRegionCell> Cells { get; set; } = [];
}

/// <summary>
/// 行内小框定义。
/// </summary>
public sealed class SmartBpRegionCell
{
    /// <summary>
    /// 小框标识（如 name / d1..d5）。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 相对于所属大框（Row.BigRect）的坐标。
    /// </summary>
    public RelativeRect Rect { get; set; }
}

/// <summary>
/// SmartBp 比例对比信息。
/// </summary>
public sealed class SmartBpAspectInfo
{
    /// <summary>
    /// 当前生效配置的完整路径。
    /// </summary>
    public string ConfigPath { get; set; } = string.Empty;

    /// <summary>
    /// 配置声明的支持比例。
    /// </summary>
    public string ConfigAspectRatio { get; set; } = "-";

    /// <summary>
    /// 当前捕获帧比例（客户端区域）。
    /// </summary>
    public string CurrentCaptureAspectRatio { get; set; } = "-";

    /// <summary>
    /// 两者是否匹配（带容差）。
    /// </summary>
    public bool IsMatched { get; set; }
}
