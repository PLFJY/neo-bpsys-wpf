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

    /// <summary>
    /// 是否在更新后显示提示信息。
    /// </summary>
    public bool ShowAfterUpdateTip { get; set; } = true;

    /// <summary>
    /// 是否记录全局禁用角色。
    /// </summary>
    public bool IsRecordGlobalBan { get; set; } = true;

    /// <summary>
    /// 是否启用经典模式（旧版 BP 流程）。
    /// </summary>
    public bool IsClassicMode { get; set; } = false;

    /// <summary>
    /// 是否将 <c>.bpui</c> 布局包文件关联到本应用。
    /// </summary>
    public bool AssociateBpuiFiles { get; set; } = true;

    /// <summary>
    /// 是否启用后台页面切换时的过渡动画。关闭后页面切换将立即完成。
    /// </summary>
    public bool IsPageTransitionAnimationEnabled { get; set; } = true;

    /// <summary>
    /// 当前选择的 OCR 模型标识键。
    /// </summary>
    public string? OcrModelKey { get; set; }

    [ObservableProperty]
    public partial string GhProxyMirror { get; set; } = "https://ghproxy.net/";

    [ObservableProperty]
    public partial string PluginMarketSource { get; set; } = "https://bpsys-plugin-index.plfjy.top/";

    [ObservableProperty]
    public partial bool IsFindPreRelease { get; set; } =
#if BETA
        true;
#else
        false;
#endif

    [ObservableProperty]
    public partial AppLogLevel LogLevel { get; set; } = AppLogLevel.Warning;

    private LanguageKey _language = LanguageKey.System;

    private CultureInfo _cultureInfo = SystemCulture;

    /// <summary>
    /// 应用程序界面语言。
    /// </summary>
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
