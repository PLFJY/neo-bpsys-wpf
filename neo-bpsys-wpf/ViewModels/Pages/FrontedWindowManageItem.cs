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
/// 前台管理页显示的前台窗口卡片。
/// </summary>
public sealed class FrontedWindowManageItem : ObservableObject
{
    /// <summary>
    /// 稳定的运行时窗口 Canonical ID。
    /// </summary>
    public string WindowId { get; init; } = string.Empty;

    /// <summary>
    /// 面向用户的窗口显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 面向用户的注册类型标签，只根据 <see cref="FrontedWindowRegistrationKind"/> 推导，与来源分组独立。
    /// </summary>
    public string KindDisplay { get; init; } = string.Empty;

    /// <summary>
    /// 此窗口是否可由设计器 v3 自定义。
    /// </summary>
    public bool CanCustomize { get; init; }

    /// <summary>
    /// 此窗口是否来自当前布局包的用户自定义窗口清单。
    /// </summary>
    public bool IsCustom { get; init; }

    /// <summary>
    /// 此条目是否为用户自定义窗口分组末尾的创建窗口卡片。
    /// </summary>
    public bool IsCreatePlaceholder { get; init; }

    /// <summary>
    /// 根据注册表注册创建卡片条目。
    /// </summary>
    /// <param name="registration">窗口注册。</param>
    /// <param name="settingsHostService">可选的设置服务，用于解析本地化的窗口显示名称。</param>
    /// <returns>用于前台管理页的卡片条目。</returns>
    public static FrontedWindowManageItem FromRegistration(
        FrontedWindowRegistration registration,
        ISettingsHostService? settingsHostService = null)
    {
        return new FrontedWindowManageItem
        {
            WindowId = registration.Id,
            DisplayName = GetRegistrationDisplayName(registration, settingsHostService),
            KindDisplay = registration.Kind == FrontedWindowRegistrationKind.Xaml
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "FrontManageWindowCategory.Xaml")
                : I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "FrontManageWindowCategory.V3Layout"),
            CanCustomize = registration.Kind == FrontedWindowRegistrationKind.V3Layout,
            IsCustom = registration is FrontedCustomV3LayoutWindowRegistration
        };
    }

    /// <summary>
    /// 创建用户自定义窗口分组末尾的新建窗口卡片。
    /// </summary>
    /// <returns>只提供新建命令的创建卡片。</returns>
    public static FrontedWindowManageItem CreateCustomWindowPlaceholder()
    {
        return new FrontedWindowManageItem
        {
            IsCustom = true,
            IsCreatePlaceholder = true
        };
    }

    private static string GetRegistrationDisplayName(
        FrontedWindowRegistration registration,
        ISettingsHostService? settingsHostService)
    {
        // 内置窗口的本地化显示名由 UI 层通过现有 resx（Designer.Window.{LocalId}）解析；
        // 非内置窗口使用注册 DisplayName / LocalId 回退。
        if (registration.IsBuiltIn)
        {
            var resxKey = $"Designer.Window.{registration.LocalId}";
            var localized = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, resxKey);
            if (!string.Equals(localized, resxKey, StringComparison.Ordinal))
            {
                return localized;
            }
        }

        var settings = settingsHostService?.Settings;
        return FrontedWindowDisplayNameResolver.ResolveDisplayName(
            registration,
            settings?.Language ?? LanguageKey.System,
            settings?.CultureInfo);
    }
}
