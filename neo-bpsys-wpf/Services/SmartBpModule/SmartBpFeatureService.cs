using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.SmartBpModule;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>
/// 面向宿主的 SmartBP 功能代理。
/// </summary>
public sealed class SmartBpFeatureService : ISmartBpFeatureService
{
    private readonly SmartBpModuleManager _moduleManager;

    /// <summary>
    /// 初始化 <see cref="SmartBpFeatureService"/> 类的新实例。
    /// </summary>
    /// <param name="moduleManager">模块管理器。</param>
    public SmartBpFeatureService(SmartBpModuleManager moduleManager)
    {
        _moduleManager = moduleManager;
        _moduleManager.ModuleStateChanged += (_, _) => ModuleStateChanged?.Invoke(this, EventArgs.Empty);
        _moduleManager.PostGameRecognitionProgressChanged += (_, e) => PostGameRecognitionProgressChanged?.Invoke(this, e);
    }

    /// <inheritdoc />
    public event EventHandler? ModuleStateChanged;

    /// <inheritdoc />
    public event EventHandler<SmartBpPostGameRecognitionProgressEventArgs>? PostGameRecognitionProgressChanged;

    /// <inheritdoc />
    public bool IsModuleLoaded => _moduleManager.IsModuleLoaded;

    /// <inheritdoc />
    public SmartBpPostGameRecognitionProgress CurrentPostGameRecognitionProgress
        => _moduleManager.CurrentPostGameRecognitionProgress;

    /// <inheritdoc />
    public Task AutoFillGameDataAsync(CancellationToken cancellationToken = default)
    {
        return _moduleManager.ExecuteFeatureCommandAsync(
            SmartBpModuleConstants.AutoFillGameDataCommandId,
            cancellationToken);
    }
}
