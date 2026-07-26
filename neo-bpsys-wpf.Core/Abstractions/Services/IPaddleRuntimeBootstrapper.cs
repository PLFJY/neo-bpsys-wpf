namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Paddle runtime 启动引导器。在 SmartBp 模块加载前、任何 PaddleInference native P/Invoke 前执行，
/// 选择并加载唯一的 native runtime（CPU 或 CUDA）。
/// </summary>
public interface IPaddleRuntimeBootstrapper
{
    /// <summary>
    /// 执行启动引导。根据用户偏好、CUDA 设备检测、组件安装状态和命令行参数，
    /// 选择唯一 native runtime 目录并加载。
    /// </summary>
    /// <param name="forceCpu">是否强制使用 CPU（来自 <c>--force-cpu-ocr</c> 命令行参数）。</param>
    void Bootstrap(bool forceCpu);
}
