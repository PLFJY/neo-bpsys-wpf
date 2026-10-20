using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.ViewModels;

/// <summary>
/// 验证设计器切换窗口时保存布局的窗口元数据契约。
/// </summary>
public sealed class FrontedDesignerWindowSettingsSwitchTest
{
    /// <summary>
    /// 保存旧窗口布局时，保留自身的译名和尺寸；加载新窗口后使用新窗口的译名。
    /// </summary>
    /// <param name="source">旧窗口布局的来源。</param>
    /// <param name="hasDisplayNames">旧窗口是否声明译名。</param>
    /// <returns>断言完成时结束的任务。</returns>
    [Theory]
    [InlineData(FrontedLayoutSource.BuiltIn, true)]
    [InlineData(FrontedLayoutSource.User, true)]
    [InlineData(FrontedLayoutSource.User, false)]
    public async Task SavingCurrentLayoutDuringWindowSwitchPreservesItsWindowMetadata(
        FrontedLayoutSource source,
        bool hasDisplayNames)
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var registry = new Mock<IFrontedWindowRegistry>();
            registry.Setup(x => x.GetV3LayoutWindows()).Returns(
            [
                new FrontedV3LayoutWindowRegistration
                {
                    Id = "ScoreSurWindow", LocalId = "ScoreSurWindow", DisplayName = "Survivor score", IsBuiltIn = true
                },
                new FrontedV3LayoutWindowRegistration
                {
                    Id = "BpWindow", LocalId = "BpWindow", DisplayName = "BP", IsBuiltIn = true
                }
            ]);

            var layout = new Mock<IFrontedLayoutService>();
            var displayNames = hasDisplayNames
                ? new Dictionary<string, string>
                {
                    ["zh_Hans"] = "求生者比分",
                    ["en_US"] = "Survivor score",
                    ["ja_JP"] = "サバイバースコア"
                }
                : new Dictionary<string, string>();
            layout.Setup(x => x.LoadWindowConfigWithMetadataAsync("ScoreSurWindow", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FrontedLayoutLoadResult
                {
                    Config = new FrontedWindowConfig
                    {
                        DisplayNames = displayNames,
                        WindowSettings = new FrontedWindowSettings { WindowWidth = 480, WindowHeight = 152 },
                        CanvasSettings = new FrontedCanvasSettings { CanvasWidth = 480, CanvasHeight = 152 }
                    },
                    Source = source,
                    Path = "ScoreSurWindow.json"
                });
            var bpDisplayNames = new Dictionary<string, string> { ["en_US"] = "Draft" };
            layout.Setup(x => x.LoadWindowConfigWithMetadataAsync("BpWindow", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FrontedLayoutLoadResult
                {
                    Config = new FrontedWindowConfig { DisplayNames = bpDisplayNames },
                    Source = FrontedLayoutSource.User,
                    Path = "BpWindow.json"
                });
            FrontedWindowConfig? savedConfig = null;
            string? savedWindowId = null;
            layout.Setup(x => x.SaveWindowConfigAsync(
                    It.IsAny<string>(), It.IsAny<FrontedWindowConfig>(), It.IsAny<CancellationToken>()))
                .Callback<string, FrontedWindowConfig, CancellationToken>((windowId, config, _) =>
                {
                    savedWindowId = windowId;
                    savedConfig = config;
                })
                .Returns(Task.CompletedTask);

            var behaviors = new Mock<IFrontedBehaviorService>();
            behaviors.Setup(x => x.LoadDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FrontedBehaviorDocument());
            var localization = new Mock<IFrontedDesignerLocalizationService>();
            var viewModel = new FrontedDesignerWindowViewModel(
                new FrontedDesignerLayoutCatalog(registry.Object),
                layout.Object,
                new FrontedLayoutDesignConverter(),
                new FrontedLayoutValidator(),
                new FrontedLayoutReferenceScanner(),
                new FrontedPropertyGridBuilder(),
                new FrontedControlDefaultConfigFactory(),
                new FrontedControlNameGenerator(),
                localization.Object,
                new DesignerPreviewSharedDataService(),
                new Mock<IFrontedLocalResourceStore>().Object,
                new Mock<IFrontedWindowLayoutOptionsService>().Object,
                new Mock<IFrontedLayoutPackageManager>().Object,
                new Mock<IFrontedWindowService>().Object,
                behaviors.Object,
                new FrontedBehaviorClipboard(),
                new FrontedBehaviorCopyPasteService(
                    new FrontedBehaviorControlSemanticResolver(), localization.Object),
                new Mock<IFrontedAnimationRuntime>().Object,
                null!,
                NullLogger<FrontedDesignerWindowViewModel>.Instance,
                v3ControlRegistry: new FrontedV3ControlRegistry(Array.Empty<FrontedV3ControlRegistration>()));

            await viewModel.ReloadLayoutCoreAsync();
            Assert.NotNull(viewModel.CurrentDocument);
            viewModel.CurrentDocument.IsDirty = true;

            viewModel.SelectWindow("BpWindow");
            Assert.True(await viewModel.SaveCurrentLayoutAsync());

            Assert.NotNull(savedConfig);
            Assert.Equal("ScoreSurWindow", savedWindowId);
            Assert.Equal(displayNames, savedConfig.DisplayNames);
            Assert.Equal(480, savedConfig.WindowSettings.WindowWidth);
            Assert.Equal(152, savedConfig.WindowSettings.WindowHeight);
            Assert.Equal(480, savedConfig.CanvasSettings.CanvasWidth);
            Assert.Equal(152, savedConfig.CanvasSettings.CanvasHeight);

            await viewModel.ReloadLayoutCoreAsync();
            Assert.NotNull(viewModel.CurrentDocument);
            viewModel.CurrentDocument.IsDirty = true;
            Assert.True(await viewModel.SaveCurrentLayoutAsync());
            Assert.Equal("BpWindow", savedWindowId);
            Assert.Equal(bpDisplayNames, savedConfig.DisplayNames);
        });
    }
}
