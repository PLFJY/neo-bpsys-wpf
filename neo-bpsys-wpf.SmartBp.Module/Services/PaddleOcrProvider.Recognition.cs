using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.PaddleRuntime;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Shared;
using System.Collections;
using System.Formats.Tar;
using System.IO;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// PaddleOCR 提供者的图像识别逻辑。
/// </summary>
public sealed partial class PaddleOcrProvider
{
    /// <summary>
    /// 识别图像中的文本。
    /// </summary>
    /// <param name="img">待识别图像。</param>
    /// <returns>识别文本；失败时返回 <see langword="null"/>。</returns>
    public string? RecognizeText(Mat img)
    {
        if (img.Empty()) return null;

        // PaddleOCR 全流程更稳的是 8UC3(BGR)，现在先对其进行预处理
        if (img.Channels() == 1)
        {
            using var bgr = new Mat();
            Cv2.CvtColor(img, bgr, ColorConversionCodes.GRAY2BGR);
            return RecognizeTextCore(bgr);
        }

        return RecognizeTextCore(img);
    }

    /// <summary>
    /// 识别图像中的文本行和边界框。
    /// </summary>
    /// <param name="img">待识别图像。</param>
    /// <returns>文本行识别结果。</returns>
    public OcrTextBlockResult RecognizeTextLines(Mat img)
    {
        if (img.Empty()) return new([], string.Empty, "Paddle");

        if (img.Channels() == 1)
        {
            using var bgr = new Mat();
            Cv2.CvtColor(img, bgr, ColorConversionCodes.GRAY2BGR);
            return RecognizeTextLinesCore(bgr);
        }

        return RecognizeTextLinesCore(img);
    }

    /// <summary>
    /// 对已知的单一文本区域直接执行 Paddle 字符识别，跳过容易漏掉细窄字符的文本检测阶段。
    /// </summary>
    /// <param name="img">紧密裁剪后的单一文本区域。</param>
    /// <returns>字符识别结果；模型未就绪、输入为空或识别失败时返回 <see langword="null"/>。</returns>
    public OcrSingleTextResult? RecognizeSingleText(Mat img)
    {
        ArgumentNullException.ThrowIfNull(img);
        if (img.Empty())
            return null;

        if (img.Channels() == 1)
        {
            using var bgr = new Mat();
            Cv2.CvtColor(img, bgr, ColorConversionCodes.GRAY2BGR);
            return RecognizeSingleTextCore(bgr);
        }

        return RecognizeSingleTextCore(img);
    }

    /// <inheritdoc />
    OcrTextBlockResult IOcrProvider.RecognizeTextLines(Mat img, OcrRecognitionOptions? options) =>
        RecognizeTextLines(img);

    /// <summary>
    /// 执行 OCR 主流程，并在失败时尝试一次重建后重试。
    /// </summary>
    /// <param name="bgr">BGR 格式输入图像。</param>
    /// <returns>识别文本；失败返回 <see langword="null"/>。</returns>
    private string? RecognizeTextCore(Mat bgr)
    {
        lock (_ocrLock)
        {
            var result = RunOcrWithRetryUnsafe(bgr);
            if (result != null)
                return string.IsNullOrWhiteSpace(result.Text) ? null : NormalizeOcrText(result.Text);
        }

        ShowMissingModelWarningOnce();
        return null;
    }

    /// <summary>
    /// 执行 OCR 文本行识别，并在失败时尝试一次重建后重试。
    /// </summary>
    /// <param name="bgr">BGR 格式输入图像。</param>
    /// <returns>文本行识别结果。</returns>
    private OcrTextBlockResult RecognizeTextLinesCore(Mat bgr)
    {
        lock (_ocrLock)
        {
            var result = RunOcrWithRetryUnsafe(bgr);
            if (result != null)
                return ToTextBlockResult(result, bgr.Width, bgr.Height);
        }

        ShowMissingModelWarningOnce();
        return new([], string.Empty, "Paddle");
    }

    /// <summary>
    /// 在 Paddle OCR 锁内直接运行字符识别器。
    /// </summary>
    /// <param name="bgr">BGR 格式的单一文本区域。</param>
    /// <returns>字符识别结果；模型未就绪或识别失败时返回 <see langword="null"/>。</returns>
    private OcrSingleTextResult? RecognizeSingleTextCore(Mat bgr)
    {
        lock (_ocrLock)
        {
            if (_ocr == null)
            {
                ShowMissingModelWarningOnce();
                return null;
            }

            try
            {
                var result = _ocr.Recognizer.Run(bgr);
                var text = NormalizeOcrText(result.Text);
                var confidence = Math.Clamp(result.Score, 0, 1);
                var characters = string.Join(",", result.Chars.Select(character =>
                    $"{character.Character}:{character.Score:0.000}"));
                _debugLog.Write(
                    "ocr-paddle",
                    $"direct_recognition: input={bgr.Width}x{bgr.Height}; text=[{text}]; score={confidence:0.000}; chars=[{characters}].");
                return string.IsNullOrWhiteSpace(text)
                    ? null
                    : new OcrSingleTextResult(text, confidence, "Paddle/direct");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Paddle direct text recognition failed.");
                _debugLog.Write("ocr-paddle", $"direct_recognition failed: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 瞬态 GPU 故障重试前等待时间（毫秒）。PaddleOCR 在 GPU 模式下首次推理可能因显存分配
    /// 或 kernel 初始化失败，等待后重建 predictor 重试通常可以恢复。
    /// </summary>
    private const int TransientRetryDelayMs = 3000;

    /// <summary>
    /// 在持锁状态下运行 OCR。失败时区分确定性 CUDA 库加载故障和瞬态故障：
    /// 库加载故障标记需要重启且不重试；瞬态故障等待 3 秒后重建 predictor 重试一次。
    /// </summary>
    /// <param name="bgr">BGR 格式输入图像。</param>
    /// <returns>PaddleOCR 原始结果；失败或未就绪时返回 <see langword="null"/>。</returns>
    private PaddleOcrResult? RunOcrWithRetryUnsafe(Mat bgr)
    {
        if (_ocr is null)
            return null;

        try
        {
            return _ocr.Run(bgr);
        }
        catch (Exception ex)
        {
            // 确定性 CUDA runtime 故障：同进程无法切换到另一个 paddle_inference_c.dll，标记下次启动回退 CPU。
            if (IsCudaRuntimeFailure(ex))
            {
                RecordCudaFailureIfNeeded(ex);
                _logger.LogError(ex, "CUDA runtime failure detected, restart required for CPU fallback.");
                return null;
            }

            // 瞬态故障（显存不足、kernel 初始化失败等）：等待 3 秒后重建 predictor 重试。
            _logger.LogWarning(ex, "OCR run failed (transient), waiting {Delay}ms before rebuild retry.", TransientRetryDelayMs);
            Thread.Sleep(TransientRetryDelayMs);

            if (!TryRebuildCurrentOcrUnsafe())
            {
                _logger.LogError("OCR rebuild failed after transient error, recognition aborted.");
                return null;
            }

            try
            {
                return _ocr!.Run(bgr);
            }
            catch (Exception retryEx)
            {
                // 重试仍然失败。如果是确定性 CUDA runtime 故障才标记重启，否则仅记录错误。
                if (IsCudaRuntimeFailure(retryEx))
                {
                    RecordCudaFailureIfNeeded(retryEx);
                    _logger.LogError(retryEx, "CUDA runtime failure detected on retry, restart required for CPU fallback.");
                }
                else
                {
                    _logger.LogError(retryEx, "OCR retry failed after rebuild (transient).");
                }
                return null;
            }
        }
    }

    /// <summary>
    /// 在 CUDA 后端下检测到明确的 CUDA runtime 故障时，记录诊断信息并设置一次性 CPU 强制保护。
    /// 仅当 <see cref="IPaddleRuntimeState.ActiveBackend"/> 为 <see cref="OcrInferenceBackend.Cuda"/>
    /// （bootstrap 已通过 GPU Predictor probe）且异常明确指向 CUDA 库加载失败或 Paddle GPU 执行失败时才触发。
    /// 触发时设置 <see cref="Settings.ForceCpuForNextLaunch"/>（一次性消费），下次启动强制 CPU
    /// 并立即清除标记，不会永久锁死 CUDA。
    /// </summary>
    /// <param name="ex">捕获的异常。</param>
    private void RecordCudaFailureIfNeeded(Exception ex)
    {
        if (_runtimeState.ActiveBackend != OcrInferenceBackend.Cuda)
            return;

        if (!IsCudaRuntimeFailure(ex))
            return;

        _settingsHostService.Settings.LastCudaFailure = ex.Message;
        _settingsHostService.Settings.LastCudaFailureRuntimeVersion = _runtimeManifestProvider.PaddleInferenceVersion;
        _settingsHostService.Settings.ForceCpuForNextLaunch = true;
        _ = _settingsHostService.SaveConfigAsync();
        _globalRestartService.IsRestartRequired = true;
        _logger.LogError(ex, "CUDA runtime failure detected. One-shot CPU fallback set for next launch.");
    }

    /// <summary>
    /// 判断异常是否明确指向 CUDA 库的 <b>加载失败</b>（DLL 缺失/损坏），而非 GPU 运行时推理错误。
    /// 仅匹配确定性库加载故障：
    /// <list type="bullet">
    /// <item><see cref="DllNotFoundException"/> 或 <see cref="FileLoadException"/>（任何 native 库加载失败）</item>
    /// <item>消息中同时包含加载失败关键字（"unable to load"/"dll not found"/"could not load"/"module could not be found"）
    /// 和 CUDA 库名（cublas/cudnn/cudart/cudart64/nvcuda/paddle_inference）</item>
    /// </list>
    /// 不匹配瞬态 GPU 错误（如 <c>CUBLAS_STATUS_ALLOC_FAILED</c>、<c>CUDNN_STATUS_ALLOC_FAILED</c>），
    /// 这些是显存不足或 kernel 初始化失败，可以通过等待后重建 predictor 重试恢复。
    /// </summary>
    /// <param name="ex">待判断的异常。</param>
    /// <returns>明确为 CUDA 库加载故障返回 <see langword="true"/>；瞬态 GPU 错误或其他异常返回 <see langword="false"/>。</returns>
    private static bool IsCudaLibraryLoadFailure(Exception ex)
    {
        // 确定性库加载异常类型
        if (ex is DllNotFoundException or FileLoadException)
            return true;

        var text = ex.GetType().Name + " " + (ex.Message ?? string.Empty);

        // 消息中必须同时包含"加载失败"语义和"CUDA 库名"才判定为库加载故障
        var hasLoadFailure = text.Contains("unable to load", StringComparison.OrdinalIgnoreCase)
            || text.Contains("dll not found", StringComparison.OrdinalIgnoreCase)
            || text.Contains("could not load", StringComparison.OrdinalIgnoreCase)
            || text.Contains("the specified module could not be found", StringComparison.OrdinalIgnoreCase);

        var hasCudaLibrary = text.Contains("cublas", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cudnn", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cudart", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cudart64", StringComparison.OrdinalIgnoreCase)
            || text.Contains("nvcuda", StringComparison.OrdinalIgnoreCase)
            || text.Contains("paddle_inference", StringComparison.OrdinalIgnoreCase);

        return hasLoadFailure && hasCudaLibrary;
    }

    /// <inheritdoc />
    public void PauseDownload()
    {
        _currentDownload?.Pause();
        RaiseDownloadStateChanged();
    }

    /// <inheritdoc />
    public void ResumeDownload()
    {
        _currentDownload?.Resume();
        RaiseDownloadStateChanged();
    }

    /// <summary>
    /// 判断 CUDA runtime 是否已在 Paddle 实际执行阶段失败。
    /// </summary>
    /// <param name="ex">待判断的异常。</param>
    /// <returns>明确为 CUDA runtime 失败返回 <see langword="true"/>。</returns>
    private bool IsCudaRuntimeFailure(Exception ex)
    {
        if (_runtimeState.ActiveBackend != OcrInferenceBackend.Cuda)
            return false;

        if (IsCudaLibraryLoadFailure(ex))
            return true;

        // Paddle status code 2 在 Predictor 已成功构造后由 Run() 返回，表示 GPU 执行链
        // （CUDA/cuDNN/kernel）不可用；继续重建同一 CUDA Predictor 不会恢复。
        return ex.ToString().Contains("Paddle status code 2", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 将 PaddleOCR 原始结果转换为稳定排序的文本块结果。
    /// </summary>
    /// <param name="result">PaddleOCR 原始结果。</param>
    /// <returns>文本块结果。</returns>
    private OcrTextBlockResult ToTextBlockResult(PaddleOcrResult result, int inputWidth, int inputHeight)
    {
        _debugLog.Write(
            "ocr-paddle",
            $"raw_result: input={inputWidth}x{inputHeight}; region_count={result.Regions.Count()}.");
        foreach (var (region, index) in result.Regions.Select((region, index) => (region, index)))
        {
            _debugLog.Write(
                "ocr-paddle",
                $"raw_region[{index}]: text=[{region.Text}]; score={region.Score:0.000}; rect=[{region.Rect}].");
        }

        var lines = result.Regions
            .Select(region =>
            {
                var text = NormalizeOcrText(region.Text);
                if (string.IsNullOrWhiteSpace(text))
                    return null;

                var originalBox = region.Rect.BoundingRect();
                var boundingBox = ClampToInput(originalBox, inputWidth, inputHeight);
                if (boundingBox != originalBox)
                    _logger.LogWarning(
                        "Paddle OCR returned an out-of-bounds box. input={Width}x{Height}; original={Original}; clamped={Clamped}",
                        inputWidth, inputHeight, originalBox, boundingBox);
                var centerX = boundingBox.X + boundingBox.Width / 2d;
                var centerY = boundingBox.Y + boundingBox.Height / 2d;
                _logger.LogDebug(
                    "provider=Paddle; input={Width}x{Height}; line bbox={Box}; center={CenterX:0.0},{CenterY:0.0}",
                    inputWidth, inputHeight, boundingBox, centerX, centerY);
                return new OcrTextLine(
                    text,
                    Math.Clamp(region.Score, 0, 1),
                    boundingBox,
                    centerX,
                    centerY,
                    "Paddle");
            })
            .Where(line => line != null)
            .Cast<OcrTextLine>()
            .OrderBy(line => line.CenterY)
            .ThenBy(line => line.CenterX)
            .ToArray();

        if (lines.Length == 0)
            return new([], string.Empty, "Paddle");

        return new OcrTextBlockResult(lines, string.Join(Environment.NewLine, lines.Select(line => line.Text)), "Paddle");
    }

    /// <summary>
    /// 将 OCR 返回的边界框裁剪到输入图像范围内。
    /// </summary>
    /// <param name="box">OCR 返回的原始边界框。</param>
    /// <param name="width">输入图像宽度。</param>
    /// <param name="height">输入图像高度。</param>
    /// <returns>裁剪后的边界框。</returns>
    private static Rect ClampToInput(Rect box, int width, int height)
    {
        var left = Math.Clamp(box.Left, 0, width);
        var top = Math.Clamp(box.Top, 0, height);
        var right = Math.Clamp(box.Right, left, width);
        var bottom = Math.Clamp(box.Bottom, top, height);
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// 规范化 OCR 文本，便于本地规则解析。
    /// </summary>
    /// <param name="text">OCR 原始文本。</param>
    /// <returns>规范化文本。</returns>
    private static string NormalizeOcrText(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Normalize(NormalizationForm.FormKC).Trim();
}
