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
/// 整条调用链只使用注册表中的 Canonical ID。
/// </summary>
public class FrontedWindowServiceCanonicalIdTest
{
    /// <summary>大小写变体创建、显示、隐藏同一窗口，状态与事件使用注册的 canonical ID。</summary>
    /// <returns>窗口生命周期验证任务。</returns>
    [Fact]
    public async Task CaseVariantLifecycle_UsesSameWindowAndCanonicalEventIdentity()
    {
        await RunOnStaThreadAsync(async () =>
        {
            const string canonicalId = "BpWindow";
            var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventBus = new Mock<IFrontedEventBus>();
            eventBus.Setup(x => x.Publish(It.IsAny<FrontedBehaviorEvent>()))
                .Callback<FrontedBehaviorEvent>(e =>
                {
                    if (e.EventType == "WindowShown") shown.TrySetResult();
                });
            var service = CreateService(CreateV3Registration(canonicalId), eventBus);
            var window = service.EnsureWindowCreated("bpwindow");
            Assert.NotNull(window);
            try
            {
                Assert.Same(window, service.EnsureWindowCreated(canonicalId));
                Assert.Contains(canonicalId, service.FrontedWindows.Keys, StringComparer.Ordinal);
                Assert.DoesNotContain("bpwindow", service.FrontedWindows.Keys, StringComparer.Ordinal);
                Assert.Same(window, service.FrontedWindows["bpwindow"]);
                Assert.False(service.FrontedWindowStates["bpwindow"]);

                foreach (var showId in new[] { "bpwindow", canonicalId })
                {
                    shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    service.ShowWindow(showId);
                    await shown.Task.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
                    Assert.True(service.FrontedWindowStates[canonicalId]);
                    Assert.Same(window, service.FrontedWindows[canonicalId]);

                    service.HideWindow(showId == canonicalId ? "bpwindow" : canonicalId);
                    Assert.False(service.FrontedWindowStates[canonicalId]);
                    Assert.False(window.IsVisible);
                    Assert.Same(window, service.FrontedWindows[canonicalId]);
                }
                foreach (var eventType in new[] { "WindowShown", "WindowHidden" })
                    eventBus.Verify(x => x.Publish(It.Is<FrontedBehaviorEvent>(e =>
                        e.EventType == eventType && e.WindowId == canonicalId)), Times.Exactly(2));
            }
            finally
            {
                CloseWindow(window);
            }
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

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        return WpfTestThread.RunAsync(action);
    }
}
