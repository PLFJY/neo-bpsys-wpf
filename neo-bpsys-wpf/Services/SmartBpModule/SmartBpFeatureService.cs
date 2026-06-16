using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.SmartBpModule;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>
/// Host-facing SmartBP feature proxy.
/// </summary>
public sealed class SmartBpFeatureService : ISmartBpFeatureService
{
    private readonly SmartBpModuleManager _moduleManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartBpFeatureService"/> class.
    /// </summary>
    /// <param name="moduleManager">Module manager.</param>
    public SmartBpFeatureService(SmartBpModuleManager moduleManager)
    {
        _moduleManager = moduleManager;
        _moduleManager.ModuleStateChanged += (_, _) => ModuleStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public event EventHandler? ModuleStateChanged;

    /// <inheritdoc />
    public bool IsModuleLoaded => _moduleManager.IsModuleLoaded;

    /// <inheritdoc />
    public Task AutoFillGameDataAsync(CancellationToken cancellationToken = default)
    {
        return _moduleManager.ExecuteFeatureCommandAsync(
            SmartBpModuleConstants.AutoFillGameDataCommandId,
            cancellationToken);
    }
}
