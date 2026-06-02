using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using System.Globalization;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 设置
/// </summary>
public partial class Settings : ObservableObjectBase
{
    private static readonly CultureInfo SystemCulture = CultureInfo.CurrentUICulture;

    /// <summary>
    /// 主设置配置版本
    /// </summary>
    public int? Version { get; set; } = 3;

    public bool ShowAfterUpdateTip { get; set; } = true;

    public bool IsRecordGlobalBan { get; set; } = true;

    public string? OcrModelKey { get; set; }

    [ObservableProperty]
    private string _ghProxyMirror = "https://ghproxy.net/";

    [ObservableProperty]
    private string _pluginMarketSource = "https://bpsys-plugin-index.plfjy.top/";

    [ObservableProperty]
    private bool _isFindPreRelease =
#if BETA
        true;
#else
        false;
#endif

    [ObservableProperty]
    private AppLogLevel _logLevel = AppLogLevel.Information;

    private LanguageKey _language = LanguageKey.System;

    private CultureInfo _cultureInfo = SystemCulture;

    public LanguageKey Language
    {
        get => _language;
        set => SetPropertyWithAction(ref _language, value, _ =>
        {
            if (value == LanguageKey.System)
            {
                CultureInfo = SystemCulture;
                return;
            }

            CultureInfo = CultureInfo.GetCultureInfo(value.ToString().Replace("_", "-"));
        });
    }

    /// <summary>
    /// 语言
    /// </summary>
    [JsonIgnore]
    public CultureInfo CultureInfo
    {
        get => _cultureInfo;
        private set => SetProperty(ref _cultureInfo, value);
    }
}
