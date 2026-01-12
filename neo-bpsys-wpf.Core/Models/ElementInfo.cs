namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 窗口中元素位置信息
/// </summary>
/// <param name="Width"></param>
/// <param name="Height"></param>
/// <param name="Left"></param>
/// <param name="Top"></param>
public record ElementInfo(double? Width, double? Height, double? Left, double? Top);