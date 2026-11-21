using System.Windows.Media;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 解析 v3 前台布局资源路径。
/// </summary>
/// <remarks>
/// 包内 <c>bpui://</c> 路径必须规范化后校验仍位于对应包根目录内；绝对文件路径仅应来自
/// 用户明确选择或受信任的布局包数据。当前默认实现已执行包根目录前缀校验。
/// </remarks>
public interface IFrontedResourceResolver
{
    /// <summary>
    /// 解析图片文件路径。
    /// </summary>
    /// <param name="path">资源路径或绝对文件路径。</param>
    /// <returns>物理文件路径；无法解析时返回 <see langword="null"/>。</returns>
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
    /// 在活动布局包变更后清除已缓存的解析资源。
    /// </summary>
    void ClearCache()
    {
    }
}
