namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 图片控件配置。
/// </summary>
public class ImageFrontedControlConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 初始化图片控件配置。
    /// </summary>
    public ImageFrontedControlConfig()
    {
        ControlType = "Image";
    }

    /// <summary>
    /// 图片控件的尺寸模式。
    /// </summary>
    public ImageSizingMode SizingMode { get; set; } = ImageSizingMode.Auto;

    /// <summary>
    /// 静态图片路径，仅在 BindingPath 为空时使用。
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// 图片拉伸方式。
    /// </summary>
    public string? Stretch { get; set; }

    /// <summary>
    /// 图片水平对齐。
    /// </summary>
    public string? HorizontalAlignment { get; set; }

    /// <summary>
    /// 图片垂直对齐。
    /// </summary>
    public string? VerticalAlignment { get; set; }

    /// <summary>
    /// 是否裁剪超出外层边框的图片内容。
    /// </summary>
    public bool ClipToBounds { get; set; }

    /// <summary>
    /// 图片圆角半径。
    /// </summary>
    public double? CornerRadius { get; set; }

    /// <summary>
    /// 是否启用锁定覆盖层。
    /// </summary>
    public bool Lockable { get; set; }

    /// <summary>
    /// 锁定覆盖层图片路径。
    /// </summary>
    public string? LockImagePath { get; set; }

    /// <summary>
    /// 锁定覆盖层可见性绑定路径。
    /// </summary>
    public string? LockVisibilityBindingPath { get; set; }

    /// <summary>
    /// 锁定覆盖层可见性规则。
    /// </summary>
    public FrontedOverlayVisibilityMode LockVisibleWhen { get; set; } = FrontedOverlayVisibilityMode.Always;

    /// <summary>
    /// 锁定覆盖层 ZIndex 偏移。
    /// </summary>
    public int LockZIndexOffset { get; set; } = 1;

    /// <summary>
    /// 是否启用选择边框覆盖层。
    /// </summary>
    public bool PickingBorderAvailable { get; set; }

    /// <summary>
    /// 选择边框图片路径。
    /// </summary>
    public string? PickingBorderImagePath { get; set; }

    /// <summary>
    /// 选择边框遮罩的填充颜色。为空时保持既有的白色填充。
    /// </summary>
    public string? PickingBorderFillColor { get; set; }

    /// <summary>
    /// 选择边框运行时名称。
    /// </summary>
    public string? PickingBorderName { get; set; }

    /// <summary>
    /// 选择边框 ZIndex 偏移。
    /// </summary>
    public int PickingBorderZIndexOffset { get; set; } = 2;

    /// <summary>
    /// 旧版 PickingBorder 名称，兼容旧 JSON。
    /// </summary>
    public bool PickingBorder
    {
        get => PickingBorderAvailable;
        set => PickingBorderAvailable = value;
    }

    /// <summary>
    /// 旧版 BanLockAvailable 名称，兼容旧 JSON。
    /// </summary>
    public bool BanLockAvailable
    {
        get => Lockable;
        set => Lockable = value;
    }

    /// <summary>
    /// 旧版 BanLockImagePath 名称，兼容旧 JSON。
    /// </summary>
    public string? BanLockImagePath
    {
        get => LockImagePath;
        set => LockImagePath = value;
    }
}
