using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Tests.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using WPFLocalizeExtension.Engine;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 验证宿主内置 v3 前台窗口的标题使用可实时刷新的本地化绑定。
/// </summary>
[Collection(WpfUiCollectionDefinition.Name)]
public sealed class FrontedV3WindowTitleLocalizationTest
{
    /// <summary>
    /// 所有内置 v3 窗口均应使用 Designer 资源中的标题，并在应用语言变化时实时更新。
    /// </summary>
    [Fact]
    public void BuiltInV3Windows_TitleBindingTracksCurrentCulture()
    {
        WpfTestThread.Run(() =>
        {
            var previousCulture = LocalizeDictionary.Instance.Culture;
            var registrations = CreateBuiltInRegistrations();
            var service = CreateService(registrations);

            try
            {
                var culture = SetCulture("zh-CN");
                var windows = registrations.ToDictionary(
                    registration => registration.LocalId,
                    registration => Assert.IsType<FrontedWindowBase>(
                        service.EnsureWindowCreated(registration.Id)));

                AssertTitles(windows, registrations, culture);

                culture = SetCulture("en-US");
                AssertTitles(windows, registrations, culture);

                culture = SetCulture("ja-JP");
                AssertTitles(windows, registrations, culture);
            }
            finally
            {
                foreach (var window in service.FrontedWindows.Values.OfType<FrontedWindowBase>())
                {
                    window.RequestServiceClose();
                }

                LocalizeDictionary.Instance.Culture = previousCulture;
            }
        });
    }

    /// <summary>
    /// 非内置 v3 窗口继续使用注册显示名，不应误用宿主的同名资源键。
    /// </summary>
    [Fact]
    public void ExternalV3Window_TitleKeepsRegistrationDisplayName()
    {
        WpfTestThread.Run(() =>
        {
            var registration = new FrontedV3LayoutWindowRegistration
            {
                Id = "BpWindow",
                LocalId = "BpWindow",
                IsBuiltIn = false,
                DisplayName = "插件 BP 窗口"
            };
            var service = CreateService([registration]);
            var window = Assert.IsType<FrontedWindowBase>(service.EnsureWindowCreated(registration.Id));

            try
            {
                Assert.Equal("插件 BP 窗口", window.Title);
            }
            finally
            {
                window.RequestServiceClose();
            }
        });
    }

    private static FrontedV3LayoutWindowRegistration[] CreateBuiltInRegistrations() =>
    [
        CreateBuiltInRegistration("BpWindow"),
        CreateBuiltInRegistration("CutSceneWindow"),
        CreateBuiltInRegistration("ScoreSurWindow"),
        CreateBuiltInRegistration("ScoreHunWindow"),
        CreateBuiltInRegistration("ScoreGlobalWindow"),
        CreateBuiltInRegistration("GameDataWindow"),
        CreateBuiltInRegistration("BpOverviewWindow"),
        CreateBuiltInRegistration("MapV2Window")
    ];

    private static FrontedV3LayoutWindowRegistration CreateBuiltInRegistration(string localId)
    {
        return new FrontedV3LayoutWindowRegistration
        {
            Id = localId,
            LocalId = localId,
            IsBuiltIn = true,
            DisplayName = localId
        };
    }

    private static void AssertTitles(
        IReadOnlyDictionary<string, FrontedWindowBase> windows,
        IEnumerable<FrontedV3LayoutWindowRegistration> registrations,
        CultureInfo culture)
    {
        foreach (var registration in registrations)
        {
            Assert.Equal(
                I18nHelper.GetLocalizedString(
                    AppI18nDictionaries.Designer,
                    $"Designer.Window.{registration.LocalId}",
                    culture),
                windows[registration.LocalId].Title);
        }
    }

    private static CultureInfo SetCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        LocalizeDictionary.Instance.Culture = culture;
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        return culture;
    }

    private static FrontedWindowService CreateService(IEnumerable<FrontedWindowRegistration> registrations)
    {
        var registrationArray = registrations.ToArray();
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IFrontedLayoutService>());
        services.AddSingleton(Mock.Of<IFrontedRenderer>());
        services.AddSingleton(Mock.Of<ISharedDataService>());
        services.AddSingleton(NullLogger<FrontedWindowBase>.Instance);

        var registry = new FrontedWindowRegistryService(registrationArray);
        var options = new Mock<IFrontedWindowLayoutOptionsService>();
        options
            .Setup(service => service.GetUserOptionsPath(It.IsAny<string>()))
            .Returns(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "window.json"));

        return new FrontedWindowService(
            services.BuildServiceProvider(),
            registry,
            options.Object,
            NullLogger<FrontedWindowService>.Instance);
    }

}
