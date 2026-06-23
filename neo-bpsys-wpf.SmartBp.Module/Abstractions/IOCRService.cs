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
/// <param name="Provider">OCR 提供程序名称。</param>
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
/// <param name="Provider">OCR 提供程序名称。</param>
public sealed record OcrTextBlockResult(IReadOnlyList<OcrTextLine> Lines, string FullText, string? Provider = null)
{
    /// <summary>
    /// 空 OCR 文本块结果。
    /// </summary>
    public static OcrTextBlockResult Empty { get; } = new([], string.Empty);
}

/// <summary>标识一个 OCR 提供程序实现。</summary>
public enum SmartBpOcrProviderKind
{
    /// <summary>PaddleOCR 提供程序。</summary>
    Paddle,
    /// <summary>Tesseract OCR 提供程序。</summary>
    Tesseract,
    /// <summary>通过 RapidOcrNet 接入的 RapidOCR 提供程序。</summary>
    Rapid
}

/// <summary>选择 SmartBP 使用的 OCR 提供程序。</summary>
public enum SmartBpOcrProviderMode
{
    /// <summary>使用 PaddleOCR。</summary>
    Paddle,
    /// <summary>使用 Tesseract OCR。</summary>
    Tesseract,
    /// <summary>使用通过 RapidOcrNet 接入的 RapidOCR。</summary>
    Rapid
}

/// <summary>一次 OCR 识别调用传递给提供程序的选项。</summary>
public sealed class OcrRecognitionOptions
{
    /// <summary>获取或设置逻辑图像区域提示。</summary>
    public string? RegionHint { get; set; }
    /// <summary>获取或设置逻辑业务字段提示。</summary>
    public string? FieldHint { get; set; }
    /// <summary>获取或设置是否优先识别中文。</summary>
    public bool PreferChinese { get; set; } = true;
    /// <summary>获取或设置是否优先识别英文。</summary>
    public bool PreferEnglish { get; set; } = true;
    /// <summary>获取或设置 Tesseract 页面分割模式编号。</summary>
    public int Psm { get; set; } = 6;
    /// <summary>获取或设置是否允许使用轻量预处理变体。</summary>
    public bool UsePreprocessingVariants { get; set; } = true;
}

/// <summary>使用单个 OCR 引擎识别带位置的文本。</summary>
public interface IOcrProvider
{
    /// <summary>获取提供程序类型。</summary>
    SmartBpOcrProviderKind Kind { get; }
    /// <summary>获取提供程序所需的运行时资产是否全部就绪。</summary>
    bool IsReady { get; }
    /// <summary>识别以 <paramref name="img"/> 为本地坐标系的文本行。</summary>
    /// <param name="img">输入图像。</param>
    /// <param name="options">可选识别提示。</param>
    /// <returns>带位置的 OCR 文本。</returns>
    OcrTextBlockResult RecognizeTextLines(Mat img, OcrRecognitionOptions? options = null);
}

/// <summary>
/// OCR 模型定义。
/// </summary>
/// <param name="Key">模型唯一标识。</param>
/// <param name="DisplayName">模型显示名称资源键。</param>
/// <param name="Description">模型描述资源键。</param>
public sealed record OcrModelDefinition(string Key, string DisplayName, string Description);

/// <summary>描述一个 OCR 提供程序的运行时就绪状态。</summary>
/// <param name="Kind">提供程序类型。</param>
/// <param name="IsReady">提供程序是否就绪。</param>
/// <param name="DataPath">可选运行时数据目录。</param>
/// <param name="Details">面向人工阅读的诊断详情。</param>
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
    /// <summary>获取当前显式选择的 OCR 提供程序。</summary>
    SmartBpOcrProviderKind SelectedProvider { get; }

    /// <summary>获取指定提供程序的运行时就绪状态。</summary>
    /// <param name="kind">提供程序类型。</param>
    /// <returns>提供程序状态。</returns>
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
