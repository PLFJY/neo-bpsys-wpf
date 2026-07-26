using Microsoft.Extensions.Logging;
using Sdcb.PaddleInference;

namespace neo_bpsys_wpf.SmartBp.Module.PaddleRuntime;

/// <summary>
/// Paddle CUDA runtime 真实可用性探测。
/// 在 bootstrap 加载 CUDA native DLL 成功后、标记 <c>ActiveBackend = Cuda</c> 前，
/// 创建一个真实的 GPU <see cref="PaddlePredictor"/> 验证 CUDA runtime 能正常初始化 GPU 上下文。
/// 仅 <c>LoadLibrary</c> 成功不代表 Predictor 能在 GPU 上跑（runtime 与驱动/cuDNN 不匹配、
/// 显存初始化失败等只在实际创建 Predictor 时才暴露），probe 失败则回退 CPU。
/// </summary>
internal static class PaddleRuntimeProbe
{
    /// <summary>
    /// 探测指定 CUDA 设备上能否成功创建 GPU Predictor。
    /// 不设置模型文件：probe 只验证 <see cref="PaddleConfig.EnableUseGpu"/> + <see cref="PaddleConfig.CreatePredictor"/>
    /// 能否成功初始化 CUDA 上下文，不依赖任何 OCR 模型。
    /// </summary>
    /// <param name="deviceId">目标 CUDA 设备 ID。</param>
    /// <param name="logger">日志记录器。</param>
    /// <returns>probe 成功返回 <see langword="true"/>；失败返回 <see langword="false"/>。</returns>
    public static bool TryProbeCudaPredictor(int deviceId, ILogger? logger)
    {
        try
        {
            using var config = new PaddleConfig();
            // 与 PaddleOcrProvider.CreatePaddleDevice 的 PaddleDevice.Gpu 参数保持一致：
            // initialMemoryMB=1024，使用默认精度（Float32）。
            config.EnableUseGpu(initialMemoryMB: 1024, deviceId: deviceId, PaddlePrecision.Float32);
            using var predictor = config.CreatePredictor();
            // 创建成功即代表 CUDA runtime 能正常初始化 GPU 上下文。
            // 不调用 predictor.Run()（无模型），避免 probe 引入模型加载开销。
            logger?.LogInformation(
                "Paddle CUDA runtime probe succeeded. DeviceId={DeviceId}", deviceId);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "Paddle CUDA runtime probe failed. DeviceId={DeviceId}. Falling back to CPU. " +
                "This typically means CUDA runtime DLLs do not match the installed NVIDIA driver " +
                "or cuDNN version.", deviceId);
            return false;
        }
    }
}
