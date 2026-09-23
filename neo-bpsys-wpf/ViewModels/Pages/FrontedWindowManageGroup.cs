using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 前台管理页显示的前台窗口分组。
/// </summary>
public sealed class FrontedWindowManageGroup
{
    /// <summary>
    /// 由窗口注册或回退规则提供的稳定分组键。
    /// </summary>
    public string GroupKey { get; init; } = string.Empty;

    /// <summary>
    /// 面向用户的分组显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 此分组是否为当前活动布局包的用户自定义窗口分组。
    /// </summary>
    public bool IsCustom { get; init; }

    /// <summary>
    /// 此分组中的窗口卡片。
    /// </summary>
    public ObservableCollection<FrontedWindowManageItem> Windows { get; } = [];

    /// <summary>
    /// 根据窗口注册构建分组后的前台管理页条目。
    /// </summary>
    /// <param name="registrations">要分组的窗口注册。</param>
    /// <param name="settingsHostService">可选的设置服务，用于解析本地化的窗口显示名称。</param>
    /// <returns>分组后的前台窗口管理条目。</returns>
    public static IReadOnlyList<FrontedWindowManageGroup> FromRegistrations(
        IEnumerable<FrontedWindowRegistration> registrations,
        ISettingsHostService? settingsHostService = null)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var groups = new List<FrontedWindowManageGroup>();
        var byKey = new Dictionary<string, FrontedWindowManageGroup>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            var key = GetStableGroupKey(registration);
            if (!byKey.TryGetValue(key, out var group))
            {
                group = new FrontedWindowManageGroup
                {
                    GroupKey = key,
                    DisplayName = GetGroupDisplayName(key),
                    IsCustom = key == "Custom"
                };

                byKey.Add(key, group);
                groups.Add(group);
            }

            group.Windows.Add(FrontedWindowManageItem.FromRegistration(registration, settingsHostService));
        }

        if (!byKey.TryGetValue("Custom", out var customGroup))
        {
            customGroup = new FrontedWindowManageGroup
            {
                GroupKey = "Custom",
                DisplayName = GetGroupDisplayName("Custom"),
                IsCustom = true
            };
            byKey.Add("Custom", customGroup);
            groups.Add(customGroup);
        }

        // 创建入口始终作为最后一张卡片保留；已有窗口会自然排在它前面。
        customGroup.Windows.Add(FrontedWindowManageItem.CreateCustomWindowPlaceholder());

        return groups
            .OrderBy(group => group.GroupKey switch
            {
                "BuiltIn" => 0,
                "Custom" => 1,
                "Plugin" => 2,
                "External" => 3,
                _ => 4
            })
            .ToArray();
    }

    private static string GetStableGroupKey(FrontedWindowRegistration registration)
    {
        if (registration is FrontedCustomV3LayoutWindowRegistration)
        {
            return "Custom";
        }

        if (registration.IsBuiltIn)
        {
            return "BuiltIn";
        }

        return registration.PackageId is not null ? "Plugin" : "External";
    }

    private static string GetGroupDisplayName(string groupKey)
    {
        return groupKey switch
        {
            "BuiltIn" => I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "SystemBuiltIn"),
            "Custom" => I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CustomWindows"),
            "Plugin" => I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "Plugins"),
            "External" => I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "External"),
            _ => groupKey
        };
    }
}
