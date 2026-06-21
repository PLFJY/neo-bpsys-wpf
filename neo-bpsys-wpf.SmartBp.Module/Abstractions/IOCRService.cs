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
/// <param name="Provider">OCR Provider 名称。</param>
public sealed record OcrTextLine(
    string Text,
    double Confidence,
    Rect BoundingBox,
    double CenterX,
    double CenterY,
    string? Provider = null);

/// <summary>
/// OCR 文本块识别结果。
/// </summary>
/// <param name="Lines">按纵向、横向顺序排列的文本行。</param>
/// <param name="FullText">合并后的完整文本。</param>
/// <param name="Provider">OCR Provider 名称。</param>
public sealed record OcrTextBlockResult(IReadOnlyList<OcrTextLine> Lines, string FullText, string? Provider = null)
{
    /// <summary>
    /// 空 OCR 文本块结果。
    /// </summary>
    public static OcrTextBlockResult Empty { get; } = new([], string.Empty);
}

/// <summary>Identifies an OCR provider implementation.</summary>
public enum SmartBpOcrProviderKind
{
    /// <summary>PaddleOCR.</summary>
    Paddle,
    /// <summary>Tesseract OCR.</summary>
    Tesseract,
    /// <summary>RapidOCR through RapidOcrNet.</summary>
    Rapid
}

/// <summary>Selects the OCR provider used by SmartBP.</summary>
public enum SmartBpOcrProviderMode
{
    /// <summary>Use PaddleOCR.</summary>
    Paddle,
    /// <summary>Use Tesseract OCR.</summary>
    Tesseract,
    /// <summary>Use RapidOCR through RapidOcrNet.</summary>
    Rapid
}

/// <summary>Options supplied to an OCR provider for one recognition call.</summary>
public sealed class OcrRecognitionOptions
{
    /// <summary>Gets or sets the logical image-region hint.</summary>
    public string? RegionHint { get; set; }
    /// <summary>Gets or sets the logical business-field hint.</summary>
    public string? FieldHint { get; set; }
    /// <summary>Gets or sets whether Chinese recognition is preferred.</summary>
    public bool PreferChinese { get; set; } = true;
    /// <summary>Gets or sets whether English recognition is preferred.</summary>
    public bool PreferEnglish { get; set; } = true;
    /// <summary>Gets or sets the Tesseract page segmentation mode number.</summary>
    public int Psm { get; set; } = 6;
    /// <summary>Gets or sets whether inexpensive preprocessing variants may be used.</summary>
    public bool UsePreprocessingVariants { get; set; } = true;
}

/// <summary>Recognizes positioned text using one OCR engine.</summary>
public interface IOcrProvider
{
    /// <summary>Gets the provider kind.</summary>
    SmartBpOcrProviderKind Kind { get; }
    /// <summary>Gets whether all runtime assets required by the provider are ready.</summary>
    bool IsReady { get; }
    /// <summary>Recognizes text lines in coordinates local to <paramref name="img"/>.</summary>
    /// <param name="img">Input image.</param>
    /// <param name="options">Optional recognition hints.</param>
    /// <returns>Positioned OCR text.</returns>
    OcrTextBlockResult RecognizeTextLines(Mat img, OcrRecognitionOptions? options = null);
}

/// <summary>
/// OCR 模型定义。
/// </summary>
/// <param name="Key">模型唯一标识。</param>
/// <param name="DisplayName">模型显示名称资源键。</param>
/// <param name="Description">模型描述资源键。</param>
public sealed record OcrModelDefinition(string Key, string DisplayName, string Description);

/// <summary>Describes the runtime readiness of one OCR provider.</summary>
/// <param name="Kind">Provider kind.</param>
/// <param name="IsReady">Whether the provider is ready.</param>
/// <param name="DataPath">Optional runtime data directory.</param>
/// <param name="Details">Human-readable diagnostic details.</param>
public sealed record SmartBpOcrProviderStatus(
    SmartBpOcrProviderKind Kind,
    bool IsReady,
    string? DataPath,
    string Details);

/// <summary>
/// OCR 识别服务。
/// </summary>
public interface IOcrService
{
    /// <summary>Gets the explicitly selected OCR provider.</summary>
    SmartBpOcrProviderKind SelectedProvider { get; }

    /// <summary>Gets runtime readiness information for a provider.</summary>
    /// <param name="kind">Provider kind.</param>
    /// <returns>Provider status.</returns>
    SmartBpOcrProviderStatus GetProviderStatus(SmartBpOcrProviderKind kind);

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
