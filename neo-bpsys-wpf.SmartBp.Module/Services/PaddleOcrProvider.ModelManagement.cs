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
/// PaddleOCR 提供者的模型管理逻辑。
/// </summary>
public sealed partial class PaddleOcrProvider
{
    /// <summary>
    /// 在后台线程加载用户偏好 OCR 模型。应在页面显示完毕后调用，避免与 UI 线程
    /// 的渲染/DLL 加载竞争 Windows loader lock。
    /// </summary>
    public void StartLoadingPreferredModel()
    {
        if (_isModelLoading || _ocr != null)
            return;
        _ = Task.Run(TryLoadPreferredModel);
    }

    /// <inheritdoc />
    public SmartBpOcrProviderKind Kind => SmartBpOcrProviderKind.Paddle;

    /// <inheritdoc />
    public bool IsReady
    {
        get
        {
            lock (_ocrLock)
                return _ocr != null;
        }
    }

    /// <summary>
    /// 获取可用 OCR 模型定义列表。
    /// </summary>
    /// <returns>模型定义列表。</returns>
    public IReadOnlyList<OcrModelDefinition> GetAvailableModels() =>
    [
        .. SmartBpOcrModelRegistry.Models.Select(m => new OcrModelDefinition(
            m.Key,
            m.DisplayNameKey,
            m.DescriptionKey))
    ];

    /// <summary>
    /// 判断指定模型是否已安装。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <returns>已安装返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public bool IsModelInstalled(string modelKey) => SmartBpOcrModelRegistry.IsModelInstalled(modelKey);

    /// <summary>
    /// 下载并解压指定 OCR 模型。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    public async Task DownloadModelAsync(string modelKey, CancellationToken cancellationToken = default)
    {
        if (!SmartBpOcrModelRegistry.TryGet(modelKey, out var definition))
        {
            throw new InvalidOperationException(Lf("SmartBpOcrUnsupportedModelFormat", modelKey));
        }

        lock (_downloadLock)
        {
            if (IsDownloading)
            {
                throw new InvalidOperationException(L("SmartBpOcrDownloadAlreadyInProgress"));
            }

            IsDownloading = true;
            DownloadProgress = null;
            DownloadStatusText = L("SmartBpOcrDownloadPreparing");
            _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            RaiseDownloadStateChanged();
        }

        try
        {
            await DownloadAndExtractModelAssetAsync(
                PickModelSourceUri(
                    definition.DetModel,
                    L("SmartBpOcrDetModelMetadataEmpty")),
                SmartBpOcrModelRegistry.GetDetDirectory(definition.Key),
                L("SmartBpOcrDownloadStageDet"),
                stepIndex: 1,
                stepCount: 3,
                _downloadCts.Token);

            await DownloadAndExtractModelAssetAsync(
                PickModelSourceUri(
                    definition.ClsModel,
                    L("SmartBpOcrClsModelMetadataEmpty")),
                SmartBpOcrModelRegistry.GetClsDirectory(definition.Key),
                L("SmartBpOcrDownloadStageCls"),
                stepIndex: 2,
                stepCount: 3,
                _downloadCts.Token);

            if (definition.RecModel == null)
            {
                _logger.LogError("Recognition model metadata is empty.");
                throw new InvalidOperationException(L("SmartBpOcrRecModelMetadataEmpty"));
            }
            var recModel = definition.RecModel;

            await DownloadAndExtractModelAssetAsync(
                PickModelSourceUri(
                    recModel,
                    L("SmartBpOcrRecModelMetadataEmpty")),
                SmartBpOcrModelRegistry.GetRecDirectory(definition.Key),
                L("SmartBpOcrDownloadStageRec"),
                stepIndex: 3,
                stepCount: 3,
                _downloadCts.Token);

            if (recModel.Version != ModelVersion.V5)
            {
                if (string.IsNullOrWhiteSpace(recModel.DictName))
                {
                    throw new InvalidOperationException(L("SmartBpOcrDictNameEmpty"));
                }

                var dicts = SharedUtils.LoadDicts(recModel.DictName);
                Directory.CreateDirectory(SmartBpOcrModelRegistry.GetModelDirectory(definition.Key));
                File.WriteAllLines(SmartBpOcrModelRegistry.GetRecDictPath(definition.Key), dicts, Encoding.UTF8);
            }

            DownloadProgress = 100;
            DownloadStatusText = L("SmartBpOcrDownloadCompleted");
            RaiseDownloadStateChanged();
        }
        catch (OperationCanceledException)
        {
            DownloadProgress = null;
            DownloadStatusText = L("SmartBpOcrDownloadCanceled");
            RaiseDownloadStateChanged();
            throw;
        }
        catch
        {
            CleanupModelDownloadResidue(definition.Key);
            DownloadProgress = null;
            DownloadStatusText = L("SmartBpOcrDownloadFailedSimple");
            RaiseDownloadStateChanged();
            throw;
        }
        finally
        {
            lock (_downloadLock)
            {
                IsDownloading = false;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }

            RaiseDownloadStateChanged();
        }
    }

    /// <summary>
    /// 取消当前下载任务。
    /// </summary>
    public void CancelDownload()
    {
        lock (_downloadLock)
        {
            _downloadCts?.Cancel();
        }

    }

    /// <summary>
    /// 尝试删除指定模型及其本地缓存文件。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="errorMessage">失败时的错误信息。</param>
    /// <returns>删除成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public bool TryDeleteModel(string modelKey, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!SmartBpOcrModelRegistry.TryGet(modelKey, out _))
        {
            errorMessage = Lf("SmartBpOcrUnsupportedModelFormat", modelKey);
            return false;
        }

        lock (_downloadLock)
        {
            if (IsDownloading)
            {
                errorMessage = L("SmartBpOcrDeleteBlockedByDownloading");
                return false;
            }
        }

        try
        {
            var deletingCurrent = CurrentOcrModelKey == modelKey;
            if (deletingCurrent)
            {
                lock (_ocrLock)
                {
                    _ocr?.Dispose();
                    _ocr = null;
                }

                CurrentOcrModelKey = null;
            }

            var modelDirectory = SmartBpOcrModelRegistry.GetModelDirectory(modelKey);
            if (Directory.Exists(modelDirectory))
            {
                Directory.Delete(modelDirectory, recursive: true);
            }

            if (_settingsHostService.Settings.OcrModelKey == modelKey)
            {
                _settingsHostService.Settings.OcrModelKey = null;
                _ = _settingsHostService.SaveConfigAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = Lf("SmartBpOcrDeleteFailedFormat", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 基于 <see cref="IPaddleRuntimeState.ActiveBackend"/> 创建 Paddle 设备配置。
    /// 统一所有 PaddleOcrAll 创建路径的后端决策，避免分散硬编码 Mkldnn。
    /// </summary>
    /// <returns>Paddle 设备配置委托。</returns>
    private Action<PaddleConfig> CreatePaddleDevice()
    {
        return _runtimeState.ActiveBackend switch
        {
            OcrInferenceBackend.Cuda => PaddleDevice.Gpu(
                initialMemoryMB: 1024,
                deviceId: _runtimeState.ActiveCudaDeviceId),
            _ => PaddleDevice.Mkldnn()
        };
    }

    /// <summary>
    /// 尝试切换当前 OCR 模型并加载推理实例。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="errorMessage">失败时的错误信息。</param>
    /// <returns>切换成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public bool TrySwitchOcrModel(string modelKey, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!SmartBpOcrModelRegistry.TryGet(modelKey, out var definition))
        {
            errorMessage = Lf("SmartBpOcrUnsupportedModelFormat", modelKey);
            return false;
        }

        if (!SmartBpOcrModelRegistry.IsModelInstalled(modelKey))
        {
            errorMessage = L("SmartBpOcrModelFilesIncomplete");
            return false;
        }

        try
        {
            var fullModel = BuildLocalFullModel(modelKey, definition);
            var deviceConfig = CreatePaddleDevice();

            // 记录真实 Predictor 创建的后端信息，补全 "DLL 加载成功" 与 "模型在 GPU 上可用" 之间的日志断点。
            _logger.LogInformation(
                "Creating PaddleOcrAll predictor. Model={Model}; Backend={Backend}; CudaDeviceId={DeviceId}; NativeModule={Module}",
                modelKey,
                _runtimeState.ActiveBackend,
                _runtimeState.ActiveCudaDeviceId,
                _runtimeState.LoadedNativeModulePath);

            var nextOcr = new PaddleOcrAll(fullModel, deviceConfig);

            lock (_ocrLock)
            {
                _ocr?.Dispose();
                _ocr = nextOcr;
                _ocr.AllowRotateDetection = false;
                _ocr.Enable180Classification = false;
            }

            // PaddleOcrAll 构造成功 = det/cls/rec 三个 Predictor 均在当前后端成功创建。
            // 仅此时才标记后端已验证，避免 UI 在 Predictor 实际不可用时宣称 CUDA 已启用。
            if (_runtimeState is PaddleRuntimeState concreteState)
            {
                concreteState.SetPaddleBackendVerified(true);
            }

            _missingModelWarningShown = 0;
            CurrentOcrModelKey = modelKey;
            PersistCurrentModel(modelKey);
            return true;
        }
        catch (Exception ex)
        {
            // Predictor 构造失败：标记后端未验证，并记录诊断信息。
            // 不在此处设置 ForceCpuForNextLaunch（构造失败可能是模型问题而非 CUDA runtime 问题），
            // 仅在 RecordCudaFailureIfNeeded 判定为确定性 CUDA runtime 故障时才触发一次性回退。
            if (_runtimeState is PaddleRuntimeState concreteState)
            {
                concreteState.SetPaddleBackendVerified(false);
            }
            errorMessage = Lf("SmartBpOcrLoadFailedFormat", ex.Message);
            return false;
        }
    }
}
