namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// SmartBP 所需 CUDA/cuDNN 可再发行依赖的安装状态。
/// </summary>
public enum PaddleCudaPrerequisiteInstallStatus
{
    /// <summary>尚未安装。</summary>
    NotInstalled,

    /// <summary>已安装且通过文件完整性检查。</summary>
    Installed,

    /// <summary>已安装，但内容不完整或不匹配。</summary>
    Invalid
}

/// <summary>
/// SmartBP CUDA/cuDNN 可再发行依赖的当前状态快照。
/// </summary>
/// <param name="Status">安装状态。</param>
/// <param name="IsBusy">是否正在下载或安装。</param>
/// <param name="DownloadProgress">当前总下载进度，范围 0-100；非下载阶段为 0。</param>
/// <param name="DownloadSpeed">当前下载速度（字节/秒）；非下载阶段为 <see langword="null"/>。</param>
/// <param name="CurrentStep">当前处理步骤的稳定标识。</param>
/// <param name="ErrorMessage">最近失败原因；非失败时为 <see langword="null"/>。</param>
/// <param name="IsPaused">当前网络下载是否已暂停。</param>
public sealed record PaddleCudaPrerequisiteSetupStatus(
    PaddleCudaPrerequisiteInstallStatus Status,
    bool IsBusy,
    double DownloadProgress,
    double? DownloadSpeed,
    string? CurrentStep,
    string? ErrorMessage,
    bool IsPaused = false);

/// <summary>
/// 管理 SmartBP CUDA OCR 所需的系统级 NVIDIA CUDA/cuDNN 前置条件。
/// </summary>
public interface IPaddleCudaPrerequisiteSetupService
{
    /// <summary>
    /// 获取当前状态快照。
    /// </summary>
    PaddleCudaPrerequisiteSetupStatus Status { get; }

    /// <summary>
    /// 获取已验证的 DLL 搜索目录。
    /// </summary>
    /// <returns>可安全加入进程 DLL 搜索路径的目录；依赖未安装时为空。</returns>
    IReadOnlyList<string> GetDllSearchDirectories();

    /// <summary>
    /// 下载并安装与指定 Paddle CUDA runtime 匹配的 NVIDIA 可再发行依赖。
    /// </summary>
    /// <param name="package">目标 Paddle CUDA runtime 包。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>安装流程任务。完成后请检查 <see cref="Status"/>。</returns>
    Task InstallAsync(PaddleRuntimePackageInfo package, CancellationToken cancellationToken);

    /// <summary>
    /// 暂停当前网络下载。
    /// </summary>
    void PauseDownload();

    /// <summary>
    /// 恢复当前网络下载。
    /// </summary>
    void ResumeDownload();

    /// <summary>
    /// 状态变化时触发。
    /// </summary>
    event EventHandler? StatusChanged;
}
