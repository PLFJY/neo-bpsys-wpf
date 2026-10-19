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
/// <param name="DownloadProgress">下载器报告的当前文件进度，范围 0-100；非下载阶段为 0。</param>
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
/// 管理 SmartBP CUDA OCR 所需的 NVIDIA CUDA/cuDNN 依赖；优先复用系统安装，否则部署到模块目录。
/// </summary>
public interface IPaddleCudaPrerequisiteSetupService
{
    /// <summary>
    /// 获取最近一次安装操作的状态快照；初始快照使用 CUDA 11.8 规则。设备相关检测请使用带包参数的入口。
    /// </summary>
    PaddleCudaPrerequisiteSetupStatus Status { get; }

    /// <summary>检测指定 Paddle 包所需的系统或模块私有依赖，不修改当前安装操作的状态。</summary>
    /// <param name="package">目标 Paddle CUDA runtime 包。</param>
    /// <returns>该包对应的 CUDA/cuDNN 安装状态；未支持的版本组合返回 Invalid。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="package"/> 为 null。</exception>
    /// <exception cref="NotSupportedException">旧服务实现未提供按包检测能力。</exception>
    PaddleCudaPrerequisiteInstallStatus GetInstallStatus(PaddleRuntimePackageInfo package)
    {
        ArgumentNullException.ThrowIfNull(package);
        throw new NotSupportedException("This CUDA dependency service does not support package-specific detection.");
    }

    /// <summary>
    /// 获取 CUDA 11.8 / cuDNN 8.9 已验证的 DLL 搜索目录。
    /// </summary>
    /// <returns>可安全加入进程 DLL 搜索路径的目录；依赖未安装时为空。</returns>
    IReadOnlyList<string> GetDllSearchDirectories();

    /// <summary>获取指定 Paddle 包已验证的 DLL 搜索目录，优先返回匹配的系统目录。</summary>
    /// <param name="package">目标 Paddle CUDA runtime 包。</param>
    /// <returns>仅包含该包对应的已验证目录；依赖未就绪时为空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="package"/> 为 null。</exception>
    /// <exception cref="NotSupportedException">旧服务实现未提供按包获取搜索目录的能力。</exception>
    IReadOnlyList<string> GetDllSearchDirectories(PaddleRuntimePackageInfo package)
    {
        ArgumentNullException.ThrowIfNull(package);
        throw new NotSupportedException("This CUDA dependency service does not support package-specific DLL search directories.");
    }

    /// <summary>
    /// 复用已就绪的依赖，否则将指定 Paddle 包所需的 NVIDIA 可再发行运行库下载、校验并解压到模块目录。
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
