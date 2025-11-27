using neo_bpsys.Core.Models;

namespace neo_bpsys.Core.Abstractions.Services;

public interface ISettingsHostService
{
    Settings Settings { get; set; }
    void SaveConfig();
    void LoadConfig();
    void ResetConfig();
    event EventHandler<Settings> SettingsChanged;
}
