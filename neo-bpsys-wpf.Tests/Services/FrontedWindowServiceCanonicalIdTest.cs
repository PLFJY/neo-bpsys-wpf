using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 验证 <see cref="FrontedWindowService"/> 在使用大小写不同的 Canonical ID 变体时，
/// 整条调用链只使用注册表中的 Canonical ID（Task 1.1）。
/// </summary>
public class FrontedWindowServiceCanonicalIdTest
{
    /// <summary>
    /// Task 1.1：注册 "BpWindow"，用 "bpwindow" 调用 EnsureWindowCreated 后，
    /// 缓存键应为注册时的 canonical 形式 "BpWindow"，而非调用方传入的小写变体。
    /// </summary>
    [Fact]
    public async Task EnsureWindowCreated_CaseVariantUsesRegisteredCanonicalId()
    {
        await RunOnStaThreadAsync(async () =>
        {
            const string canonicalId = "BpWindow";
            var registration = CreateV3Registration(canonicalId);
            var service = CreateService(registration);

            // 用小写变体调用，应规范化为 canonical ID 后创建并缓存。
            var window = service.EnsureWindowCreated("bpwindow");

            Assert.NotNull(window);
            // 缓存的实际存储键应为 canonical 形式 "BpWindow"，而非调用方传入的 "bpwindow"。
            // 注意：由于字典使用 OrdinalIgnoreCase，ContainsKey 对两种形式都返回 true，
            // 因此用 Ordinal 比较遍历实际键集合来验证存储形式。
            var storedKeys = service.FrontedWindows.Keys;
            Assert.Contains(canonicalId, storedKeys, StringComparer.Ordinal);
            Assert.DoesNotContain("bpwindow", storedKeys, StringComparer.Ordinal);

            CloseWindow(window!);

            await Task.CompletedTask;
        });
    }

    /// <summary>
    /// Task 1.1：注册 "BpWindow"，EnsureWindowCreated 后用 "bpwindow" 调用 HideWindow，
    /// 应能找到同一实例并更新状态。
    /// </summary>
    [Fact]
    public async Task HideWindow_CaseVariantFindsSameInstance()
    {
        await RunOnStaThreadAsync(async () =>
        {
            const string canonicalId = "BpWindow";
            var registration = CreateV3Registration(canonicalId);
            var eventBus = new Mock<IFrontedEventBus>();
            var service = CreateService(registration, eventBus: eventBus);

            var window = service.EnsureWindowCreated(canonicalId);
            Assert.NotNull(window);
            // 通过服务 ShowWindow 设置已显示状态（fire-and-forget，需等待状态更新）。
            service.ShowWindow(canonicalId);
            await WaitForWindowStateAsync(service, canonicalId, expected: true);

            // 用小写变体调用 HideWindow，应规范化为 canonical ID 后命中缓存。
            service.HideWindow("bpwindow");

            Assert.False(service.FrontedWindowStates[canonicalId]);
            Assert.False(window.IsVisible);
            // 应发布 WindowHidden 事件，payload 使用 canonical ID。
            eventBus.Verify(
                x => x.Publish(It.Is<FrontedBehaviorEvent>(e =>
                    e.EventType == "WindowHidden" && e.WindowId == canonicalId)),
                Times.Once);

            CloseWindow(window);
            await Task.CompletedTask;
        });
    }

    /// <summary>
    /// Task 1.1：验证 <see cref="FrontedWindowService.FrontedWindows"/> 和
    /// <see cref="FrontedWindowService.FrontedWindowStates"/> 使用 OrdinalIgnoreCase 比较器，
    /// 大小写不同的键应被视为同一键。
    /// </summary>
    [Fact]
    public async Task RuntimeCaches_UseCanonicalComparer()
    {
        await RunOnStaThreadAsync(async () =>
        {
            const string canonicalId = "BpWindow";
            var registration = CreateV3Registration(canonicalId);
            var service = CreateService(registration);

            var window = service.EnsureWindowCreated(canonicalId);
            Assert.NotNull(window);

            // 缓存键为 canonical ID，但用小写变体查找应能命中（OrdinalIgnoreCase）。
            Assert.True(service.FrontedWindows.TryGetValue("bpwindow", out var sameWindow));
            Assert.Same(window, sameWindow);
            Assert.True(service.FrontedWindowStates.TryGetValue("bpwindow", out var state));
            Assert.False(state);

            // 通过服务 ShowWindow 用小写变体设置已显示状态，验证 OrdinalIgnoreCase 比较器
            // 使小写变体与 canonical 键命中同一缓存条目。
            service.ShowWindow("bpwindow");
            await WaitForWindowStateAsync(service, canonicalId, expected: true);

            CloseWindow(window!);
            await Task.CompletedTask;
        });
    }

    /// <summary>
    /// Task 5.2：注册 "BpWindow"，用大小写变体交替调用 Show 和 Hide，
    /// 验证整条调用链使用同一窗口实例和同一状态条目。
    /// </summary>
    [Fact]
    public async Task CaseVariantShowHide_UsesSameWindowAndState()
    {
        await RunOnStaThreadAsync(async () =>
        {
            const string canonicalId = "BpWindow";
            var registration = CreateV3Registration(canonicalId);
            var service = CreateService(registration);

            // 用 canonical ID 创建窗口。
            var window = service.EnsureWindowCreated(canonicalId);
            Assert.NotNull(window);

            // 用小写变体 Show，应命中同一缓存条目。
            service.ShowWindow("bpwindow");
            await WaitForWindowStateAsync(service, canonicalId, expected: true);
            Assert.True(service.FrontedWindowStates[canonicalId]);
            Assert.Same(window, service.FrontedWindows[canonicalId]);

            // 用 canonical ID Hide，应命中同一缓存条目并更新状态。
            service.HideWindow(canonicalId);
            Assert.False(service.FrontedWindowStates[canonicalId]);
            Assert.Same(window, service.FrontedWindows[canonicalId]);

            CloseWindow(window!);
            await Task.CompletedTask;
        });
    }

    private static FrontedV3LayoutWindowRegistration CreateV3Registration(string id)
    {
        return new FrontedV3LayoutWindowRegistration
        {
            Id = id,
            LocalId = id,
            IsBuiltIn = false,
            DisplayName = id
        };
    }

    private static FrontedWindowService CreateService(
        FrontedWindowRegistration registration,
        Mock<IFrontedEventBus>? eventBus = null)
    {
        var services = new ServiceCollection();
        var layoutService = new Mock<IFrontedLayoutService>();
        layoutService
            .Setup(x => x.LoadWindowConfigAsync(registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateConfig("#00000000", allowsTransparency: false));
        services.AddSingleton(layoutService.Object);
        services.AddSingleton(new Mock<IFrontedRenderer>().Object);
        services.AddSingleton(Mock.Of<ISharedDataService>());
        services.AddSingleton(NullLogger<FrontedWindowBase>.Instance);

        var registry = new FrontedWindowRegistryService(new[] { registration });
        var options = new Mock<IFrontedWindowLayoutOptionsService>();
        options
            .Setup(x => x.GetUserOptionsPath(It.IsAny<string>()))
            .Returns(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "window.json"));
        options
            .Setup(x => x.LoadOptions(It.IsAny<string>()))
            .Returns(new FrontedWindowLayoutOptions());

        return new FrontedWindowService(
            services.BuildServiceProvider(),
            registry,
            options.Object,
            NullLogger<FrontedWindowService>.Instance,
            eventBus?.Object);
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

    private static void CloseWindow(Window window)
    {
        if (window is FrontedWindowBase frontedWindow)
        {
            frontedWindow.RequestServiceClose();
        }
        else
        {
            window.Close();
        }
    }

    /// <summary>
    /// 轮询等待指定窗口的状态达到预期值。用于配合 fire-and-forget 的
    /// <see cref="FrontedWindowService.ShowWindow(string)"/> 等异步状态变更。
    /// </summary>
    private static async Task WaitForWindowStateAsync(
        FrontedWindowService service,
        string windowId,
        bool expected)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (service.FrontedWindowStates.GetValueOrDefault(windowId) == expected)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(
            service.FrontedWindowStates.GetValueOrDefault(windowId) == expected,
            $"Timed out waiting for window state. WindowId: {windowId}, Expected: {expected}, " +
            $"Actual: {service.FrontedWindowStates.GetValueOrDefault(windowId)}");
    }

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        return WpfTestThread.RunAsync(action);
    }
}
