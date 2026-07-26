namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 面向宿主的 SmartBP 功能服务。
/// </summary>
public interface ISmartBpFeatureService
{
    /// <summary>
    /// 模块加载状态变化时触发。
    /// </summary>
    event EventHandler? ModuleStateChanged;

    /// <summary>
    /// SmartBP 模块当前是否已加载。
    /// </summary>
    bool IsModuleLoaded { get; }

    /// <summary>
    /// 当 SmartBP 模块已加载时，运行赛后数据自动回填。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    Task AutoFillGameDataAsync(CancellationToken cancellationToken = default);
}
