namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// CUDA runtime 组件安装状态。
/// </summary>
public enum PaddleRuntimeInstallStatus
{
    /// <summary>
    /// 未安装。
    /// </summary>
    NotInstalled,

    /// <summary>
    /// 已安装且与当前 PaddleInference 版本兼容。
    /// </summary>
    Installed,

    /// <summary>
    /// 已安装但版本与当前 PaddleInference 不兼容。
    /// </summary>
    VersionMismatch
}

/// <summary>
/// CUDA runtime 组件安装信息。
/// </summary>
/// <param name="Status">安装状态。</param>
/// <param name="PackageId">已安装包 ID。</param>
/// <param name="PackageVersion">已安装包版本。</param>
/// <param name="ComputeCapability">目标 Compute Capability（如 <c>8.6</c>）。</param>
/// <param name="InstalledAt">安装时间。</param>
/// <param name="PackageHash">包 SHA-256 哈希。</param>
/// <param name="NativeDirectory">native 文件目录绝对路径。</param>
/// <param name="Verified">安装 manifest 是否标记为已验证。</param>
public sealed record PaddleRuntimeInstallInfo(
    PaddleRuntimeInstallStatus Status,
    string? PackageId,
    string? PackageVersion,
    string? ComputeCapability,
    DateTimeOffset? InstalledAt,
    string? PackageHash,
    string? NativeDirectory,
    bool Verified);

/// <summary>
/// Paddle CUDA runtime 组件管理服务。负责下载、校验、安装、删除和状态检查。
/// </summary>
public interface IPaddleRuntimeComponentService
{
    /// <summary>
    /// 获取当前 CUDA 组件安装状态。
    /// </summary>
    /// <returns>安装信息。</returns>
    PaddleRuntimeInstallInfo GetInstallStatus();

    /// <summary>
    /// 检查已安装组件是否与当前 PaddleInference 版本兼容。
    /// </summary>
    /// <returns>兼容返回 <see langword="true"/>；未安装或不兼容返回 <see langword="false"/>。</returns>
    bool IsCompatibleWithCurrentVersion();

    /// <summary>
    /// 启动指定 CUDA runtime 包的下载。下载与随后的校验、安装均在后台异步进行，
    /// 调用方不应 <see langword="await"/> 下载结果；应通过 <see cref="DownloadStateChanged"/>
    /// 事件以及 <see cref="IsDownloading"/>、<see cref="IsDownloadFinished"/>、
    /// <see cref="LastInstallSucceeded"/> 属性感知状态。
    /// 参照 <c>UpdaterService.DownloadUpdate</c> 的 fire-and-forget 模式。
    /// </summary>
    /// <param name="package">要安装的包信息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已启动下载的任务（<see cref="Task.CompletedTask"/>）；不代表下载完成。</returns>
    Task DownloadAsync(
        PaddleRuntimePackageInfo package,
        CancellationToken cancellationToken);

    /// <summary>
    /// 取消正在进行的下载。
    /// </summary>
    void CancelDownload();

    /// <summary>
    /// 删除已安装的 CUDA 组件。
    /// </summary>
    /// <returns>删除是否成功。</returns>
    bool DeleteComponent();

    /// <summary>
    /// 当前是否正在下载。
    /// </summary>
    bool IsDownloading { get; }

    /// <summary>
    /// 当前下载是否已完成（含校验与安装）。参照 <c>UpdaterService.IsDownloadFinished</c>。
    /// </summary>
    bool IsDownloadFinished { get; }

    /// <summary>
    /// 上一次下载安装的结果：<see langword="null"/> 表示尚未完成过一次安装，
    /// <see langword="true"/> 表示成功，<see langword="false"/> 表示失败或取消。
    /// </summary>
    bool? LastInstallSucceeded { get; }

    /// <summary>
    /// 当前下载进度（0-100）；未下载时为 <see langword="null"/>。
    /// </summary>
    double? DownloadProgress { get; }

    /// <summary>
    /// 当前下载速度（字节/秒）；未下载时为 <see langword="null"/>。
    /// </summary>
    double? DownloadSpeed { get; }

    /// <summary>
    /// 下载状态变化事件。
    /// </summary>
    event EventHandler? DownloadStateChanged;
}
