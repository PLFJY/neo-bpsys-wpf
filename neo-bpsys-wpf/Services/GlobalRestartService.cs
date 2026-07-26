using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System;
namespace neo_bpsys_wpf.Services;

/// <summary>
/// <see cref="IGlobalRestartService"/> 的实现
/// </summary>
public sealed class GlobalRestartService : IGlobalRestartService
{
    private bool _isRestartRequired;

    /// <inheritdoc/>
    public bool IsRestartRequired
    {
        get => _isRestartRequired;
        set
        {
            if (_isRestartRequired == value)
            {
                return;
            }

            _isRestartRequired = value;
            RestartRequiredStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public event EventHandler? RestartRequiredStateChanged;
}
