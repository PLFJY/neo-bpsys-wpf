using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedWindowServiceTransparencyRestartTest
{
    [Fact]
    public async Task RestartWindowForTransparencyChangeAsync_NeverCreatedWindow_ReturnsFalseWithoutCreatingWindow()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var registration = CreateRegistration();
            var eventBus = new Mock<IFrontedEventBus>();
            var service = CreateService(registration, eventBus: eventBus);

            var restarted = await service.RestartWindowForTransparencyChangeAsync(registration.Id);

            Assert.False(restarted);
            Assert.Empty(service.FrontedWindows);
            Assert.Empty(service.FrontedWindowStates);
            eventBus.Verify(x => x.Publish(It.IsAny<FrontedBehaviorEvent>()), Times.Never);
        });
    }

    [Fact]
    public async Task RestartWindowForTransparencyChangeAsync_CreatedHiddenWindow_RemovesOldInstanceWithoutShowingNewWindow()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var registration = CreateRegistration();
            var eventBus = new Mock<IFrontedEventBus>();
            var service = CreateService(registration, eventBus: eventBus);
            var oldWindow = service.EnsureWindowCreated(registration.Id);
            Assert.NotNull(oldWindow);
            oldWindow.Show();
            oldWindow.Hide();
            // 状态在 EnsureWindowCreated 时由 RegisterFrontedWindow 初始化为 false，无需再手动设置。

            var restarted = await service.RestartWindowForTransparencyChangeAsync(registration.Id);

            Assert.True(restarted);
            Assert.DoesNotContain(registration.Id, service.FrontedWindows.Keys);
            Assert.DoesNotContain(registration.Id, service.FrontedWindowStates.Keys);
            Assert.False(oldWindow.IsVisible);
            eventBus.Verify(
                x => x.Publish(It.Is<FrontedBehaviorEvent>(e => e.EventType == "WindowShown")),
                Times.Never);
            eventBus.Verify(
                x => x.Publish(It.Is<FrontedBehaviorEvent>(e => e.EventType == "WindowHidden")),
                Times.Never);
        });
    }

    [Fact]
    public async Task RestartWindowForTransparencyChangeAsync_VisibleWindow_RecreatesAndShowsWindow()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var registration = CreateRegistration();
            var eventBus = new Mock<IFrontedEventBus>();
            var service = CreateService(registration, allowsTransparency: true, eventBus: eventBus);
            var oldWindow = service.EnsureWindowCreated(registration.Id);
            Assert.NotNull(oldWindow);
            SetWindowStateInternal(service, registration.Id, true);
            oldWindow.Show();

            var restarted = await service.RestartWindowForTransparencyChangeAsync(registration.Id);

            Assert.True(restarted);
            Assert.True(service.FrontedWindowStates[registration.Id]);
            Assert.True(service.FrontedWindows.TryGetValue(registration.Id, out var newWindow));
            Assert.NotSame(oldWindow, newWindow);
            Assert.True(newWindow.IsVisible);
            Assert.True(newWindow.AllowsTransparency);
            eventBus.Verify(
                x => x.Publish(It.Is<FrontedBehaviorEvent>(e => e.EventType == "WindowHidden")),
                Times.Once);
            eventBus.Verify(
                x => x.Publish(It.Is<FrontedBehaviorEvent>(e => e.EventType == "WindowShown")),
                Times.Once);

            if (newWindow is FrontedWindowBase frontedWindow)
            {
                frontedWindow.RequestServiceClose();
            }
            else
            {
                newWindow.Close();
            }
        });
    }

    [Fact]
    public async Task ReloadFrontedLayoutsAsync_ReappliesBackgroundAndRendersCurrentControls()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var registration = CreateRegistration();
            var currentConfig = CreateConfig("#FFFF0000", allowsTransparency: false);
            var renderer = new Mock<IFrontedRenderer>();
            var service = CreateService(
                registration,
                configFactory: () => currentConfig,
                renderer: renderer);
            var window = Assert.IsType<FrontedWindowBase>(service.EnsureWindowCreated(registration.Id));
            await window.EnsureInitialWindowSettingsAppliedAsync();
            window.Show();
            SetWindowStateInternal(service, registration.Id, true);

            currentConfig = CreateConfig("#FF00FF00", allowsTransparency: false);
            await service.ReloadFrontedLayoutsAsync();

            renderer.Verify(
                x => x.RenderToCanvas(
                    It.IsAny<System.Windows.Controls.Canvas>(),
                    currentConfig,
                    It.IsAny<FrontedRenderContext>()),
                Times.Once);
            window.RequestServiceClose();
        });
    }

    [Fact]
    public void DesignerTransparencyOptionDoesNotExposeRestartPrompt()
    {
        var root = GetRepositoryRoot();
        var designerXaml = File.ReadAllText(Path.Combine(
            root,
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml"));
        var designerCode = File.ReadAllText(Path.Combine(
            root,
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml.cs"));
        var designerViewModel = File.ReadAllText(Path.Combine(
            root,
            "neo-bpsys-wpf",
            "ViewModels",
            "Windows",
            "FrontedDesignerWindowViewModel.cs"));

        Assert.DoesNotContain("RestartNowButton_OnClick", designerXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RestartNowButton_OnClick", designerCode, StringComparison.Ordinal);
        Assert.Contains("RestartWindowForTransparencyChangeAsync", designerViewModel, StringComparison.Ordinal);
    }

    /// <summary>
    /// Task 2.3：XAML singleton 注册不得进入透明度重建链路。
    /// XAML 窗口在 DI 中注册为 singleton，Close() 后 DI 仍返回同一已关闭实例，
    /// WPF Window 关闭后无法再次 Show。因此 <see cref="FrontedWindowService.RestartWindowForTransparencyChangeAsync"/>
    /// 必须对 <see cref="FrontedXamlWindowRegistration"/> 返回 <see langword="false"/>，
    /// 不执行 Close/重建。
    /// </summary>
    [Fact]
    public async Task XamlTransparencyRestart_IsRejected()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var registration = CreateXamlRegistration();
            var eventBus = new Mock<IFrontedEventBus>();
            var service = CreateServiceForXaml(registration, eventBus: eventBus);

            // 先通过 EnsureWindowCreated 创建窗口实例并模拟已显示状态。
            var oldWindow = service.EnsureWindowCreated(registration.Id);
            Assert.NotNull(oldWindow);
            SetWindowStateInternal(service, registration.Id, true);
            oldWindow!.Show();

            // 调用透明度重建，应被拒绝（返回 false）。
            var restarted = await service.RestartWindowForTransparencyChangeAsync(registration.Id);

            Assert.False(restarted, "XAML registration 必须被透明度重建拒绝。");

            // 旧窗口应仍存在于缓存中且引用不变（未被 Close、未被替换）。
            Assert.True(service.FrontedWindows.TryGetValue(registration.Id, out var cachedWindow));
            Assert.Same(oldWindow, cachedWindow);
            // 状态应保持为已显示。
            Assert.True(service.FrontedWindowStates[registration.Id]);
            // 旧窗口应仍然可见（未被 Close）。
            Assert.True(oldWindow.IsVisible);
            // 不应发布任何 WindowHidden/WindowShown 事件。
            eventBus.Verify(
                x => x.Publish(It.Is<FrontedBehaviorEvent>(e => e.EventType == "WindowHidden")),
                Times.Never);
            eventBus.Verify(
                x => x.Publish(It.Is<FrontedBehaviorEvent>(e => e.EventType == "WindowShown")),
                Times.Never);

            // 清理：关闭窗口。
            oldWindow.Close();
        });
    }

    private static FrontedV3LayoutWindowRegistration CreateRegistration()
    {
        return new FrontedV3LayoutWindowRegistration
        {
            Id = Guid.NewGuid().ToString("D"),
            LocalId = "TestWindow",
            IsBuiltIn = false,
            DisplayName = "Test Window"
        };
    }

    private static FrontedXamlWindowRegistration CreateXamlRegistration()
    {
        return new FrontedXamlWindowRegistration
        {
            Id = Guid.NewGuid().ToString("D"),
            LocalId = "TestXamlWindow",
            IsBuiltIn = false,
            DisplayName = "Test XAML Window",
            WindowType = typeof(Window)
        };
    }

    /// <summary>
    /// 创建一个用于 XAML 窗口测试的服务实例。在 DI 中注册 <see cref="Window"/> 类型，
    /// 使 <see cref="FrontedWindowService"/> 的 <c>CreateXamlWindow</c> 能通过
    /// <c>GetRequiredService</c> 解析到窗口实例。
    /// </summary>
    private static FrontedWindowService CreateServiceForXaml(
        FrontedXamlWindowRegistration registration,
        Mock<IFrontedEventBus>? eventBus = null)
    {
        var services = new ServiceCollection();
        // 注册 Window 为 singleton，模拟 AddFrontedWindow 注册 factory 的行为。
        // CreateXamlWindow 现在仅通过 GetRequiredService 解析窗口实例。
        services.AddSingleton<Window>(_ => new Window());

        var registry = new Mock<IFrontedWindowRegistry>();
        FrontedWindowRegistration registrationOut = registration;
        registry
            .Setup(x => x.TryGet(registration.Id, out registrationOut))
            .Returns(true);

        var options = new Mock<IFrontedWindowLayoutOptionsService>();
        options
            .Setup(x => x.GetUserOptionsPath(It.IsAny<string>()))
            .Returns(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "window.json"));
        options
            .Setup(x => x.LoadOptions(It.IsAny<string>()))
            .Returns(new FrontedWindowLayoutOptions());

        return new FrontedWindowService(
            services.BuildServiceProvider(),
            registry.Object,
            options.Object,
            NullLogger<FrontedWindowService>.Instance,
            eventBus?.Object);
    }

    private static FrontedWindowService CreateService(
        FrontedWindowRegistration registration,
        bool allowsTransparency = false,
        Mock<IFrontedEventBus> eventBus = null,
        Func<FrontedWindowConfig> configFactory = null,
        Mock<IFrontedRenderer> renderer = null)
    {
        var services = new ServiceCollection();
        var layoutService = new Mock<IFrontedLayoutService>();
        layoutService
            .Setup(x => x.LoadWindowConfigAsync(registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => configFactory?.Invoke() ?? CreateConfig("#00000000", allowsTransparency));
        services.AddSingleton(layoutService.Object);
        services.AddSingleton((renderer ?? new Mock<IFrontedRenderer>()).Object);
        services.AddSingleton(Mock.Of<ISharedDataService>());
        services.AddSingleton(NullLogger<FrontedWindowBase>.Instance);

        var registry = new Mock<IFrontedWindowRegistry>();
        var registrationOut = registration;
        registry
            .Setup(x => x.TryGet(registration.Id, out registrationOut))
            .Returns(true);

        var options = new Mock<IFrontedWindowLayoutOptionsService>();
        options
            .Setup(x => x.GetUserOptionsPath(It.IsAny<string>()))
            .Returns(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "window.json"));
        options
            .Setup(x => x.LoadOptions(It.IsAny<string>()))
            .Returns(new FrontedWindowLayoutOptions());

        return new FrontedWindowService(
            services.BuildServiceProvider(),
            registry.Object,
            options.Object,
            NullLogger<FrontedWindowService>.Instance,
            eventBus == null ? null : eventBus.Object);
    }

    private static FrontedWindowConfig CreateConfig(string backgroundColor, bool allowsTransparency)
    {
        return new FrontedWindowConfig
        {
            WindowSettings =
            {
                WindowWidth = 320,
                WindowHeight = 180,
                AllowsTransparency = allowsTransparency,
                BackgroundColor = backgroundColor
            },
            CanvasSettings =
            {
                CanvasWidth = 320,
                CanvasHeight = 180
            }
        };
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }

    /// <summary>
    /// 通过反射设置 <see cref="FrontedWindowService"/> 内部的窗口状态字典，
    /// 用于在不触发服务公开方法副作用（事件发布、窗口渲染）的前提下设置测试所需状态。
    /// Task 3.3 将 <see cref="FrontedWindowService.FrontedWindowStates"/> 改为只读视图后，
    /// 测试不再能直接写入公开字典。
    /// </summary>
    private static void SetWindowStateInternal(FrontedWindowService service, string windowId, bool state)
    {
        var field = typeof(FrontedWindowService).GetField(
            "_frontedWindowStates",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var dict = (Dictionary<string, bool>)field!.GetValue(service)!;
        dict[windowId] = state;
    }

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        return WpfTestThread.RunAsync(action);
    }
}
