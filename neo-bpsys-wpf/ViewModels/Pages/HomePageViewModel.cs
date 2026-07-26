using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 首页视图模型，负责展示应用版本更新信息。
/// </summary>
public partial class HomePageViewModel : ViewModelBase
{
    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
#pragma warning disable CS8618
    public HomePageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    /// <summary>
    /// 初始化首页视图模型。
    /// </summary>
    /// <param name="updaterService">更新服务</param>
    /// <param name="settingsHostService">设置宿主服务</param>
    public HomePageViewModel(IUpdaterService updaterService, ISettingsHostService settingsHostService)
    {
        updaterService.NewVersionInfoChanged += (sender, args) =>
        {
            ReleaseInfo = updaterService.NewVersionInfo;
            if (string.IsNullOrEmpty(ReleaseInfo.TagName)) ReleaseNotes = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "LoadingFailed");
            else ReleaseNotes = $"# {ReleaseInfo.Name}\r\n\r\n" + ReleaseInfo.Body;
        };
        IsExpanded = settingsHostService.Settings.ShowAfterUpdateTip;
        ReleaseNotes = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "LoadingFailed");
    }

    /// <summary>
    /// 获取或设置更新日志区域是否展开。
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// 当前版本更新信息。
    /// </summary>
    [ObservableProperty]
    public partial ReleaseInfo? ReleaseInfo { get; set; }

    /// <summary>
    /// 更新日志内容（Markdown 格式）。
    /// </summary>
    [ObservableProperty]
    public partial string ReleaseNotes { get; set; } = string.Empty;
}
