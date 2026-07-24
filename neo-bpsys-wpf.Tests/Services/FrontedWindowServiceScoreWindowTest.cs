using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 验证 <see cref="FrontedWindowService"/> 对 <see cref="FrontedWindowType.ScoreWindow"/>
/// 的组合分派行为（Task 1.4）。
/// </summary>
public class FrontedWindowServiceScoreWindowTest
{
    /// <summary>
    /// Task 1.4：调用 ShowWindow(ScoreWindow) 后，三个比分窗口都应被显示。
    /// </summary>
    [Fact]
    public async Task ShowScoreWindow_ShowsAllThreeScoreWindows()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var service = CreateServiceWithScoreWindows(out var surId, out var hunId, out var globalId);

            service.ShowWindow(FrontedWindowType.ScoreWindow);

            // ShowWindow 是 fire-and-forget；轮询等待三个窗口都被显示。
            await WaitForAllShownAsync(service, surId, hunId, globalId);

            Assert.True(service.FrontedWindowStates.GetValueOrDefault(surId), "ScoreSurWindow 应被显示");
            Assert.True(service.FrontedWindowStates.GetValueOrDefault(hunId), "ScoreHunWindow 应被显示");
            Assert.True(service.FrontedWindowStates.GetValueOrDefault(globalId), "ScoreGlobalWindow 应被显示");

            CleanupWindows(service);
        });
    }

    /// <summary>
    /// Task 1.4：调用 HideWindow(ScoreWindow) 后，三个比分窗口都应被隐藏。
    /// </summary>
    [Fact]
    public async Task HideScoreWindow_HidesAllThreeScoreWindows()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var service = CreateServiceWithScoreWindows(out var surId, out var hunId, out var globalId);

            // 先显示三个窗口。
            service.ShowWindow(FrontedWindowType.ScoreWindow);
            await WaitForAllShownAsync(service, surId, hunId, globalId);

            // 再用 ScoreWindow 复合操作隐藏。
            service.HideWindow(FrontedWindowType.ScoreWindow);

            Assert.False(service.FrontedWindowStates.GetValueOrDefault(surId), "ScoreSurWindow 应被隐藏");
            Assert.False(service.FrontedWindowStates.GetValueOrDefault(hunId), "ScoreHunWindow 应被隐藏");
            Assert.False(service.FrontedWindowStates.GetValueOrDefault(globalId), "ScoreGlobalWindow 应被隐藏");

            CleanupWindows(service);
        });
    }

    /// <summary>
    /// Task 1.4：ScoreWindow 不应被解析为 Guid.Empty 注册。
    /// 调用 ShowWindow(ScoreWindow) 后，缓存中不应出现 Guid.Empty 字符串形式的键，
    /// 也不应触发针对 Guid.Empty 的未注册窗口错误提示。
    /// </summary>
    [Fact]
    public async Task ScoreWindow_IsNeverResolvedAsGuidEmptyRegistration()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var service = CreateServiceWithScoreWindows(out var surId, out var hunId, out var globalId);

            service.ShowWindow(FrontedWindowType.ScoreWindow);
            await WaitForAllShownAsync(service, surId, hunId, globalId);

            var guidEmptyString = Guid.Empty.ToString();
            // 缓存中不应出现 Guid.Empty 字符串形式的键。
            Assert.False(service.FrontedWindows.ContainsKey(guidEmptyString));
            Assert.False(service.FrontedWindowStates.ContainsKey(guidEmptyString));
            // 缓存中只应包含三个真实比分窗口（以及可能的其他注册窗口，但绝不应有 Guid.Empty）。
            Assert.DoesNotContain(guidEmptyString,
                service.FrontedWindows.Keys,
                StringComparer.Ordinal);

            CleanupWindows(service);
        });
    }

    /// <summary>
    /// Task 5.2：调用 ShowWindow(ScoreWindow) 后，应分派到三个比分窗口（ScoreSur/Hun/Global），
    /// 验证组合分派行为。
    /// </summary>
    [Fact]
    public async Task ScoreWindow_ShowDispatchesThreeWindows()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var service = CreateServiceWithScoreWindows(out var surId, out var hunId, out var globalId);

            service.ShowWindow(FrontedWindowType.ScoreWindow);
            await WaitForAllShownAsync(service, surId, hunId, globalId);

            // 三个比分窗口都应被显示。
            Assert.True(service.FrontedWindowStates.GetValueOrDefault(surId), "ScoreSurWindow 应被分派显示");
            Assert.True(service.FrontedWindowStates.GetValueOrDefault(hunId), "ScoreHunWindow 应被分派显示");
            Assert.True(service.FrontedWindowStates.GetValueOrDefault(globalId), "ScoreGlobalWindow 应被分派显示");

            CleanupWindows(service);
        });
    }

    /// <summary>
    /// Task 5.2：调用 HideWindow(ScoreWindow) 后，应分派隐藏三个比分窗口（ScoreSur/Hun/Global），
    /// 验证组合分派行为。
    /// </summary>
    [Fact]
    public async Task ScoreWindow_HideDispatchesThreeWindows()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var service = CreateServiceWithScoreWindows(out var surId, out var hunId, out var globalId);

            // 先显示三个窗口。
            service.ShowWindow(FrontedWindowType.ScoreWindow);
            await WaitForAllShownAsync(service, surId, hunId, globalId);

            // 用 ScoreWindow 复合操作隐藏。
            service.HideWindow(FrontedWindowType.ScoreWindow);

            // 三个比分窗口都应被隐藏。
            Assert.False(service.FrontedWindowStates.GetValueOrDefault(surId), "ScoreSurWindow 应被分派隐藏");
            Assert.False(service.FrontedWindowStates.GetValueOrDefault(hunId), "ScoreHunWindow 应被分派隐藏");
            Assert.False(service.FrontedWindowStates.GetValueOrDefault(globalId), "ScoreGlobalWindow 应被分派隐藏");

            CleanupWindows(service);
        });
    }

    private static FrontedWindowService CreateServiceWithScoreWindows(
        out string surId, out string hunId, out string globalId)
    {
        surId = FrontedWindowType.ScoreSurWindow.ToString();
        hunId = FrontedWindowType.ScoreHunWindow.ToString();
        globalId = FrontedWindowType.ScoreGlobalWindow.ToString();

        var registrations = new[]
        {
            CreateV3Registration(surId),
            CreateV3Registration(hunId),
            CreateV3Registration(globalId)
        };

        var services = new ServiceCollection();
        var layoutService = new Mock<IFrontedLayoutService>();
        // 对任意窗口 ID 返回一个最小配置，避免 EnsureInitialWindowSettingsAppliedAsync 抛异常。
        layoutService
            .Setup(x => x.LoadWindowConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateConfig("#00000000", allowsTransparency: false));
        services.AddSingleton(layoutService.Object);
        services.AddSingleton(new Mock<IFrontedRenderer>().Object);
        services.AddSingleton(Mock.Of<ISharedDataService>());
        services.AddSingleton(NullLogger<FrontedWindowBase>.Instance);

        var registry = new FrontedWindowRegistryService(registrations);
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
            NullLogger<FrontedWindowService>.Instance);
    }

    private static async Task WaitForAllShownAsync(
        FrontedWindowService service,
        params string[] windowIds)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (windowIds.All(id => service.FrontedWindowStates.GetValueOrDefault(id)))
            {
                return;
            }

            // 让出 dispatcher，使 fire-and-forget 的异步续体得以执行。
            await Task.Delay(20);
        }

        // 超时后让 Assert 失败暴露具体状态。
        Assert.True(
            windowIds.All(id => service.FrontedWindowStates.GetValueOrDefault(id)),
            "Timed out waiting for score windows to be shown. States: "
            + string.Join(", ", windowIds.Select(id => $"{id}={service.FrontedWindowStates.GetValueOrDefault(id)}")));
    }

    private static void CleanupWindows(FrontedWindowService service)
    {
        foreach (var window in service.FrontedWindows.Values.ToArray())
        {
            try
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
            catch
            {
                // 测试清理时忽略关闭异常。
            }
        }
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

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        return WpfTestThread.RunAsync(action);
    }
}
