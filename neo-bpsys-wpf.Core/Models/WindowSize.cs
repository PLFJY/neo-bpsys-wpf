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
    [ObservableProperty]
    public partial double Width { get; set; }

    /// <summary>
    /// 高度
    /// </summary>
    [ObservableProperty]
    public partial double Height { get; set; }

    /// <summary>
    /// 使用指定的宽度和高度构造窗体大小实例。
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
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