namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 设计器 v3 添加控件命令输入。
/// </summary>
public sealed class FrontedAddControlRequest
{
    /// <summary>
    /// 要添加的内置 v3 控件类型。
    /// </summary>
    public string ControlType { get; init; } = string.Empty;

    /// <summary>
    /// 可选的逻辑画布 X 坐标，用于新控件的中心点。
    /// </summary>
    public double? CenterX { get; init; }

    /// <summary>
    /// 可选的逻辑画布 Y 坐标，用于新控件的中心点。
    /// </summary>
    public double? CenterY { get; init; }
}
