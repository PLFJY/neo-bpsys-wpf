using OpenCvSharp;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// OCR 识别到的一行文本及其位置。
/// </summary>
/// <param name="Text">规范化后的文本。</param>
/// <param name="Confidence">识别置信度。</param>
/// <param name="BoundingBox">文本在输入图像中的轴对齐外接矩形。</param>
/// <param name="CenterX">文本中心点 X 坐标。</param>
/// <param name="CenterY">文本中心点 Y 坐标。</param>
public sealed record OcrTextLine(string Text, double Confidence, Rect BoundingBox, double CenterX, double CenterY);

/// <summary>
/// OCR 文本块识别结果。
/// </summary>
/// <param name="Lines">按纵向、横向顺序排列的文本行。</param>
/// <param name="FullText">合并后的完整文本。</param>
public sealed record OcrTextBlockResult(IReadOnlyList<OcrTextLine> Lines, string FullText)
{
    /// <summary>
    /// 空 OCR 文本块结果。
    /// </summary>
    public static OcrTextBlockResult Empty { get; } = new([], string.Empty);
}

/// <summary>
/// OCR 模型定义。
/// </summary>
/// <param name="Key">模型唯一标识。</param>
/// <param name="DisplayName">模型显示名称资源键。</param>
/// <param name="Description">模型描述资源键。</param>
public sealed record OcrModelDefinition(string Key, string DisplayName, string Description);

/// <summary>
/// OCR 识别服务。
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// 当前正在使用的 OCR 模型键。
    /// </summary>
    string? CurrentOcrModelKey { get; }

    /// <summary>
    /// 当前是否存在模型下载任务。
    /// </summary>
    bool IsDownloading { get; }

    /// <summary>
    /// 当前下载进度（0-100）；未知时为 <see langword="null"/>。
    /// </summary>
    double? DownloadProgress { get; }

    /// <summary>
    /// 当前下载状态文本。
    /// </summary>
    string DownloadStatusText { get; }

    /// <summary>
    /// 下载状态变更事件。
    /// </summary>
    event EventHandler? DownloadStateChanged;

    /// <summary>
    /// 获取可用 OCR 模型列表。
    /// </summary>
    /// <returns>模型定义列表。</returns>
    IReadOnlyList<OcrModelDefinition> GetAvailableModels();

    /// <summary>
    /// 判断指定模型是否已完整安装。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <returns>已安装返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    bool IsModelInstalled(string modelKey);

    /// <summary>
    /// 下载指定 OCR 模型。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    Task DownloadModelAsync(string modelKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消当前模型下载任务。
    /// </summary>
    void CancelDownload();

    /// <summary>
    /// 尝试删除本地 OCR 模型。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="errorMessage">失败时的错误信息。</param>
    /// <returns>删除成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    bool TryDeleteModel(string modelKey, out string errorMessage);

    /// <summary>
    /// 尝试切换当前 OCR 模型。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="errorMessage">失败时的错误信息。</param>
    /// <returns>切换成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    bool TrySwitchOcrModel(string modelKey, out string errorMessage);

    /// <summary>
    /// 识别图像中的文本。
    /// </summary>
    /// <param name="bin">待识别图像。</param>
    /// <returns>识别文本；识别失败时返回 <see langword="null"/>。</returns>
    string? RecognizeText(Mat bin);

    /// <summary>
    /// 识别图像中的文本行与边界框。
    /// </summary>
    /// <param name="img">待识别图像。</param>
    /// <returns>文本行识别结果；无文本或失败时返回空结果。</returns>
    OcrTextBlockResult RecognizeTextLines(Mat img);
}
