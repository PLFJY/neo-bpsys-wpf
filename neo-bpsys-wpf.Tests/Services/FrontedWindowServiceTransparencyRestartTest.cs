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
using System.IO;
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
            service.FrontedWindowStates[registration.Id] = false;

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
            service.FrontedWindowStates[registration.Id] = true;
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
            service.FrontedWindowStates[registration.Id] = true;

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

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        return WpfTestThread.RunAsync(action);
    }
}
