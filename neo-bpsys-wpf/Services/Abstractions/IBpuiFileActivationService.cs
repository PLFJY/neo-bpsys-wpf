namespace neo_bpsys_wpf.Services.Abstractions;

/// <summary>
/// 处理操作系统对 <c>.bpui</c> 布局包文件的激活请求。
/// </summary>
public interface IBpuiFileActivationService
{
    /// <summary>
    /// 开始监听由后续应用程序实例转发的 <c>.bpui</c> 文件路径。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    void StartListening(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止监听转发的 <c>.bpui</c> 文件路径。
    /// </summary>
    void StopListening();

    /// <summary>
    /// 将 <c>.bpui</c> 路径转发给已经在运行的应用程序实例。
    /// </summary>
    /// <param name="packagePath">包文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>路径成功转发时返回 <see langword="true"/>。</returns>
    Task<bool> TryForwardToRunningInstanceAsync(string packagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 导入并激活 <c>.bpui</c> 布局包。
    /// </summary>
    /// <param name="packagePath">包文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导入与激活结果。</returns>
    Task<BpuiFileActivationResult> OpenPackageAsync(string packagePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// 从操作系统打开 <c>.bpui</c> 包时产生的结果。
/// </summary>
/// <param name="Success">是否已导入并激活该包。</param>
/// <param name="PackageId">导入的包 ID。</param>
/// <param name="ErrorMessage">失败原因（若有）。</param>
public sealed record BpuiFileActivationResult(bool Success, string? PackageId, string? ErrorMessage);
