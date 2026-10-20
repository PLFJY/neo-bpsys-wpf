using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 为设计器 v3 布局存储编辑器本地资源。
/// </summary>
public interface IFrontedLocalResourceStore
{
    /// <summary>
    /// 将本地图片复制到编辑器本地的 bpui 资源存储中，并返回 bpui URI。
    /// </summary>
    string StoreImage(string sourcePath);

    /// <summary>
    /// 复制本地图片并返回便于编辑器会话清理的详细信息。
    /// </summary>
    FrontedLocalResourceStoreResult StoreImageWithResult(string sourcePath, bool compressOversizedImage = false);

    /// <summary>
    /// 按图片实际用途校验并复制本地图片，可在临时副本中压缩超限图片。
    /// </summary>
    /// <param name="sourcePath">本地图片文件路径。</param>
    /// <param name="purpose">图片用途，决定体积和尺寸限制。</param>
    /// <param name="compressOversizedImage">是否已获用户确认，允许压缩超限图片。</param>
    /// <returns>资源 URI、物理路径及复制和压缩结果。</returns>
    /// <exception cref="System.ArgumentException">源路径为空。</exception>
    /// <exception cref="System.IO.FileNotFoundException">源文件不存在。</exception>
    /// <exception cref="System.NotSupportedException">图片格式不受支持。</exception>
    /// <exception cref="System.IO.InvalidDataException">图片校验或压缩失败。</exception>
    FrontedLocalResourceStoreResult StoreImageWithResult(
        string sourcePath,
        FrontedImagePurpose purpose,
        bool compressOversizedImage = false);

    /// <summary>
    /// 将本地 bpui 资源 URI 解析为其物理文件路径。
    /// </summary>
    bool TryGetPhysicalPath(string resourceUri, out string physicalPath);

    /// <summary>
    /// 将本地字体复制到布局包资源存储中，并返回所复制文件的字体选项。
    /// </summary>
    /// <param name="sourcePath">源字体路径。</param>
    /// <param name="packageId">目标包 ID。</param>
    /// <param name="packageRoot">目标包根目录。</param>
    /// <returns>存储字体的结果，每个发现的字体系列对应一项。</returns>
    IReadOnlyList<FrontedLocalFontResourceStoreResult> StorePackageFontWithResult(
        string sourcePath,
        string packageId,
        string packageRoot);
}

/// <summary>
/// 存储编辑器本地资源的结果。
/// </summary>
public sealed record FrontedLocalResourceStoreResult
{
    /// <summary>
    /// 初始化本地资源存储结果。
    /// </summary>
    /// <param name="resourceUri">存储后的 bpui 资源 URI。</param>
    /// <param name="physicalPath">物理复制文件路径。</param>
    /// <param name="wasNewlyCreated">是否为新复制的文件。</param>
    /// <param name="wasCompressed">是否先在临时副本中压缩了超限图片。</param>
    public FrontedLocalResourceStoreResult(
        string resourceUri,
        string physicalPath,
        bool wasNewlyCreated,
        bool wasCompressed = false)
    {
        ResourceUri = resourceUri;
        PhysicalPath = physicalPath;
        WasNewlyCreated = wasNewlyCreated;
        WasCompressed = wasCompressed;
    }

    /// <summary>
    /// 存储后的 bpui 资源 URI。
    /// </summary>
    public string ResourceUri { get; }

    /// <summary>
    /// 物理复制文件路径。
    /// </summary>
    public string PhysicalPath { get; }

    /// <summary>
    /// 是否为新复制的文件。
    /// </summary>
    public bool WasNewlyCreated { get; }

    /// <summary>
    /// 指示源图片是否因超限而在保存前被压缩。
    /// </summary>
    public bool WasCompressed { get; }
}

/// <summary>
/// 存储包字体资源的结果。
/// </summary>
public sealed record FrontedLocalFontResourceStoreResult
{
    /// <summary>
    /// 初始化包字体存储结果。
    /// </summary>
    /// <param name="resourceUri">存储后的 bpui 字体 URI，包含字体系列片段。</param>
    /// <param name="physicalPath">物理复制字体路径。</param>
    /// <param name="wasNewlyCreated">是否为新复制的文件。</param>
    /// <param name="fontFamilyName">发现的字体系列名称。</param>
    public FrontedLocalFontResourceStoreResult(
        string resourceUri,
        string physicalPath,
        bool wasNewlyCreated,
        string fontFamilyName)
    {
        ResourceUri = resourceUri;
        PhysicalPath = physicalPath;
        WasNewlyCreated = wasNewlyCreated;
        FontFamilyName = fontFamilyName;
    }

    /// <summary>
    /// 存储后的 bpui 字体 URI，包含字体系列片段。
    /// </summary>
    public string ResourceUri { get; }

    /// <summary>
    /// 物理复制字体路径。
    /// </summary>
    public string PhysicalPath { get; }

    /// <summary>
    /// 是否为新复制的文件。
    /// </summary>
    public bool WasNewlyCreated { get; }

    /// <summary>
    /// 发现的字体系列名称。
    /// </summary>
    public string FontFamilyName { get; }
}
