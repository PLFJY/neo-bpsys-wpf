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
/// PaddleOCR 提供者的模型加载逻辑。
/// </summary>
public sealed partial class PaddleOcrProvider
{
    /// <summary>
    /// 尝试加载用户设置中的首选 OCR 模型。
    /// </summary>
    private void TryLoadPreferredModel()
    {
        string? preferredModel = _settingsHostService.Settings.OcrModelKey;

        if (string.IsNullOrWhiteSpace(preferredModel))
            return;

        _isModelLoading = true;
        RaiseModelLoadStateChanged();
        try
        {
            _ = TrySwitchOcrModel(preferredModel, out _);
        }
        finally
        {
            _isModelLoading = false;
            RaiseModelLoadStateChanged();
        }
    }

    /// <summary>
    /// 仅在首次检测到 OCR 未就绪时弹出一次提示。
    /// </summary>
    private void ShowMissingModelWarningOnce()
    {
        if (Interlocked.Exchange(ref _missingModelWarningShown, 1) == 1)
            return;

        _ = MessageBoxHelper.ShowErrorAsync(L("SmartBpOcrNotReadyFirstDownloadAndSwitchModel"));
    }

    /// <summary>
    /// 持久化当前模型键到配置。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    private void PersistCurrentModel(string modelKey)
    {
        _settingsHostService.Settings.OcrModelKey = modelKey;
        _ = _settingsHostService.SaveConfigAsync();
    }

    /// <summary>
    /// 基于本地模型目录构建完整 OCR 模型对象。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="definition">模型定义。</param>
    /// <returns>可用于推理的完整 OCR 模型。</returns>
    private FullOcrModel BuildLocalFullModel(string modelKey, SmartBpOcrModelDefinition definition)
    {
        if (definition.DetModel == null)
        {
            _logger.LogError("Detection model metadata is empty for model: {ModelKey}", modelKey);
            throw new InvalidOperationException(L("SmartBpOcrDetModelMetadataEmpty"));
        }
        var onlineDet = definition.DetModel;

        if (definition.ClsModel == null)
        {
            _logger.LogError("Classification model metadata is empty for model: {ModelKey}", modelKey);
            throw new InvalidOperationException(L("SmartBpOcrClsModelMetadataEmpty"));
        }
        var onlineCls = definition.ClsModel;

        if (definition.RecModel == null)
        {
            _logger.LogError("Recognition model metadata is empty for model: {ModelKey}", modelKey);
            throw new InvalidOperationException(L("SmartBpOcrRecModelMetadataEmpty"));
        }
        var onlineRec = definition.RecModel;

        var detModel = DetectionModel.FromDirectory(
            SmartBpOcrModelRegistry.GetDetDirectory(modelKey),
            onlineDet.Version);
        var clsModel = ClassificationModel.FromDirectory(
            SmartBpOcrModelRegistry.GetClsDirectory(modelKey),
            onlineCls.Version);

        RecognizationModel recModel = onlineRec.Version switch
        {
            ModelVersion.V5 => RecognizationModel.FromDirectoryV5(SmartBpOcrModelRegistry.GetRecDirectory(modelKey)),
            _ => RecognizationModel.FromDirectory(
                SmartBpOcrModelRegistry.GetRecDirectory(modelKey),
                SmartBpOcrModelRegistry.GetRecDictPath(modelKey),
                onlineRec.Version)
        };

        return new FullOcrModel(detModel, clsModel, recModel);
    }
}
