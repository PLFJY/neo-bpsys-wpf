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
/// PaddleOCR 提供者的推理恢复逻辑。
/// </summary>
public sealed partial class PaddleOcrProvider
{
    /// <summary>
    /// 在 OCR 推理异常后尝试重建当前模型实例。
    /// 该方法要求调用方已持有 <see cref="_ocrLock"/>。
    /// </summary>
    private bool TryRebuildCurrentOcrUnsafe()
    {
        if (string.IsNullOrWhiteSpace(CurrentOcrModelKey))
            return false;

        if (!SmartBpOcrModelRegistry.TryGet(CurrentOcrModelKey, out var definition))
            return false;

        if (!SmartBpOcrModelRegistry.IsModelInstalled(CurrentOcrModelKey))
            return false;

        try
        {
            var fullModel = BuildLocalFullModel(CurrentOcrModelKey, definition);
            var rebuilt = new PaddleOcrAll(fullModel, CreatePaddleDevice())
            {
                AllowRotateDetection = false,
                Enable180Classification = false
            };

            _ocr?.Dispose();
            _ocr = rebuilt;
            _logger.LogInformation("OCR predictor rebuilt successfully for model: {ModelKey}", CurrentOcrModelKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild OCR predictor for model: {ModelKey}", CurrentOcrModelKey);
            return false;
        }
    }
}
