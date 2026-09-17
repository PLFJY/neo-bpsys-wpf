using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 将当前活动布局包中的用户自定义窗口同步到前台窗口注册表。
/// </summary>
public sealed class FrontedCustomWindowRegistrySynchronizer
{
    private readonly IFrontedLayoutPackageManager _packageManager;
    private readonly IFrontedWindowRegistry _windowRegistry;
    private readonly ILogger<FrontedCustomWindowRegistrySynchronizer> _logger;

    /// <summary>
    /// 初始化自定义窗口注册同步器。
    /// </summary>
    /// <param name="packageManager">布局包管理器。</param>
    /// <param name="windowRegistry">前台窗口注册表。</param>
    /// <param name="logger">日志记录器。</param>
    public FrontedCustomWindowRegistrySynchronizer(
        IFrontedLayoutPackageManager packageManager,
        IFrontedWindowRegistry windowRegistry,
        ILogger<FrontedCustomWindowRegistrySynchronizer> logger)
    {
        _packageManager = packageManager;
        _windowRegistry = windowRegistry;
        _logger = logger;
    }

    /// <summary>
    /// 重新读取活动包并替换自定义窗口集合。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>同步完成后结束的任务。</returns>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var registrations = await _packageManager.GetActiveCustomWindowsAsync(cancellationToken);
            _windowRegistry.ReplaceCustomWindows(registrations);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to refresh active package custom fronted windows.");
            _windowRegistry.ReplaceCustomWindows(Array.Empty<FrontedCustomV3LayoutWindowRegistration>());
        }
    }
}
