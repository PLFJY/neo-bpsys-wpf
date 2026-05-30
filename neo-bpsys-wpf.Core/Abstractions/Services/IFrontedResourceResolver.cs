using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 解析 v3 前台布局资源路径。
/// </summary>
public interface IFrontedResourceResolver
{
    /// <summary>
    /// 解析图片文件路径。
    /// </summary>
    string? ResolveImagePath(string? path);

    /// <summary>
    /// 解析图片资源。
    /// </summary>
    ImageSource? ResolveImage(string? path);
}
