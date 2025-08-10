namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 窗口分辨率（未来也许会用得上）
/// </summary>
/// <param name="width">宽度</param>
/// <param name="height">高度</param>
public class WindowResolution(int width, int height)
{
    /// <summary>
    /// 宽度
    /// </summary>
    public int Width { get; set; } = width;
    
    /// <summary>
    /// 高度
    /// </summary>
    public int Height { get; set; } = height;
}