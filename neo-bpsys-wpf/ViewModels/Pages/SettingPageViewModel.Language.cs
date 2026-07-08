using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downloader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SettingPageViewModel : ViewModelBase
{
    #region 语言设置

    private LanguageKey _selectedLanguage = LanguageKey.System;

    /// <summary>
    /// 获取或设置当前选择的语言。更改后立即生效并保存。
    /// </summary>
    public LanguageKey SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetPropertyWithAction(ref _selectedLanguage, value, _ =>
        {
            _settingsHostService.Settings.Language = value;
            _settingsHostService.SaveConfigAsync();
            LocalizeDictionary.Instance.Culture = _settingsHostService.Settings.CultureInfo;
            Application.Current.Resources["CurrentLanguage"] =
                XmlLanguage.GetLanguage(_settingsHostService.Settings.CultureInfo.Name);
            ProductTourFontResourceHelper.Apply(_settingsHostService.Settings.CultureInfo);
            SyncMirrorFromSettings();
        });
    }

    /// <summary>
    /// 可选语言列表字典，键为本地化 Key，值为对应语言。
    /// </summary>
    public Dictionary<string, LanguageKey> LanguageList { get; } = new()
    {
        { "FollowSystem", LanguageKey.System },
        { "zh_Hans", LanguageKey.zh_Hans },
        { "en_US", LanguageKey.en_US },
        { "ja_JP", LanguageKey.ja_JP }
    };

    #endregion
}

