using System.Windows.Media;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

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
    /// 解析通用布局资源文件路径。
    /// </summary>
    /// <param name="path">资源路径。</param>
    /// <returns>物理文件路径；无法解析时返回 <see langword="null"/>。</returns>
    string? ResolveFilePath(string? path)
    {
        return ResolveImagePath(path);
    }

    /// <summary>
    /// 解析图片资源。
    /// </summary>
    ImageSource? ResolveImage(
        string? path,
        FrontedImagePurpose purpose = FrontedImagePurpose.PackageResource);

    /// <summary>
    /// Clears cached resolved resources after the active layout package changes.
    /// </summary>
    void ClearCache()
    {
    }
}
