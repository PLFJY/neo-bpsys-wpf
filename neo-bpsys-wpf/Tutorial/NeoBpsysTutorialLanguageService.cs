using System.Windows;
using System.Windows.Markup;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.ProductTour;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Applies tutorial language choices to the application settings and WPF resources.
/// </summary>
public sealed class NeoBpsysTutorialLanguageService : ITutorialLanguageService
{
    private readonly ISettingsHostService _settingsHostService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NeoBpsysTutorialLanguageService"/> class.
    /// </summary>
    /// <param name="settingsHostService">Settings host service.</param>
    public NeoBpsysTutorialLanguageService(ISettingsHostService settingsHostService)
    {
        _settingsHostService = settingsHostService;
    }

    /// <inheritdoc />
    public async Task ApplyLanguageAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        _settingsHostService.Settings.Language =
            string.Equals(cultureName, "en-US", StringComparison.OrdinalIgnoreCase)
                ? LanguageKey.en_US
                : LanguageKey.zh_Hans;
        LocalizeDictionary.Instance.Culture = _settingsHostService.Settings.CultureInfo;
        Application.Current.Resources["CurrentLanguage"] =
            XmlLanguage.GetLanguage(_settingsHostService.Settings.CultureInfo.Name);
        await _settingsHostService.SaveConfigAsync();
    }
}
