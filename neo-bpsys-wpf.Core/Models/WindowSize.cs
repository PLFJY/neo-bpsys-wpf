using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 窗体大小
/// </summary>
public partial class WindowSize: ObservableObjectBase
{
    /// <summary>
    /// 宽度
    /// </summary>
    [ObservableProperty] private double _width;

    /// <summary>
    /// 高度
    /// </summary>
    [ObservableProperty] private double _height;

    /// <summary>
    /// 构造
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns>新的窗体大小实例</returns>
    public WindowSize(double width, double height)
    {
        Width = width;
        Height = height;
    }
    
    /// <summary>
    /// 设置窗体大小
    /// </summary>
    /// <param name="other">新值</param>
    public void SetNewValue(WindowSize other)
    {
        Width = other.Width;
        Height = other.Height;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Width}x{Height}";
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is WindowSize size &&
               Math.Abs(Width - size.Width) < 0.01 &&
               Math.Abs(Height - size.Height) < 0.01;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            (int)(Width * 100), 
            (int)(Height * 100)
        );
    }
}