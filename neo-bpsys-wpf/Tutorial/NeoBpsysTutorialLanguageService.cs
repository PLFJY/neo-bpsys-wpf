using System.Windows;
using System.Windows.Markup;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.ProductTour;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// 将教程语言选择应用到应用设置和 WPF 资源。
/// </summary>
public sealed class NeoBpsysTutorialLanguageService : ITutorialLanguageService
{
    private readonly ISettingsHostService _settingsHostService;
    private static readonly IReadOnlyDictionary<string, LanguageKey> LanguageMap = new Dictionary<string, LanguageKey>
    {
        ["System"] = LanguageKey.System,
        ["zh_Hans"] = LanguageKey.zh_Hans,
        ["en_US"] = LanguageKey.en_US,
        ["ja_JP"] = LanguageKey.ja_JP
    };

    /// <inheritdoc />
    public event EventHandler? LanguageChanged;

    /// <summary>
    /// 初始化 <see cref="NeoBpsysTutorialLanguageService"/> 类的新实例。
    /// </summary>
    /// <param name="settingsHostService">设置宿主服务。</param>
    public NeoBpsysTutorialLanguageService(ISettingsHostService settingsHostService)
    {
        _settingsHostService = settingsHostService;
        _settingsHostService.LanguageSettingChanged += OnExternalLanguageChanged;
    }

    private void OnExternalLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TutorialLanguageOption>> GetLanguageOptionsAsync(CancellationToken cancellationToken = default)
    {
        var selected = _settingsHostService.Settings.Language;
        IReadOnlyList<TutorialLanguageOption> options =
        [
            new TutorialLanguageOption
            {
                Id = "System",
                DisplayName = "跟随系统",
                NativeName = "Follow system",
                IsSystemDefault = true,
                IsSelected = selected == LanguageKey.System
            },
            new TutorialLanguageOption
            {
                Id = "zh_Hans",
                DisplayName = "简体中文",
                NativeName = "简体中文",
                IsSelected = selected == LanguageKey.zh_Hans
            },
            new TutorialLanguageOption
            {
                Id = "en_US",
                DisplayName = "English",
                NativeName = "English",
                IsSelected = selected == LanguageKey.en_US
            },
            new TutorialLanguageOption
            {
                Id = "ja_JP",
                DisplayName = "日本語",
                NativeName = "日本語",
                IsSelected = selected == LanguageKey.ja_JP
            }
        ];
        return Task.FromResult(options);
    }

    /// <inheritdoc />
    public async Task ApplyLanguageAsync(string languageOptionId, CancellationToken cancellationToken = default)
    {
        _settingsHostService.Settings.Language = LanguageMap.TryGetValue(languageOptionId, out var language)
            ? language
            : LanguageKey.System;
        ApplyLocalizeDictionaryCulture();
        if (Application.Current != null)
        {
            Application.Current.Resources["CurrentLanguage"] =
                XmlLanguage.GetLanguage(_settingsHostService.Settings.CultureInfo.Name);
            ProductTourFontResourceHelper.Apply(_settingsHostService.Settings.CultureInfo);
        }

        await _settingsHostService.SaveConfigAsync();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyLocalizeDictionaryCulture()
    {
        try
        {
            LocalizeDictionary.Instance.Culture = _settingsHostService.Settings.CultureInfo;
        }
        catch (AggregateException ex) when (IsClosedDispatcherLocalizationException(ex))
        {
        }
    }

    private static bool IsClosedDispatcherLocalizationException(Exception exception) =>
        exception is TaskCanceledException
        || exception is AggregateException aggregate
        && aggregate.InnerExceptions.All(IsClosedDispatcherLocalizationException);
}
