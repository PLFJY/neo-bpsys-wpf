namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 截图方式
/// </summary>
public enum CaptureMethod
{
    /// <summary>使用 GDI BitBlt 进行截图</summary>
    Bitblt,
    /// <summary>使用 Windows Graphics Capture API 进行截图</summary>
    WGC
}