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
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.ViewModels;

/// <summary>
/// 验证设计器切换窗口时保存旧布局的窗口尺寸契约。
/// </summary>
public sealed class FrontedDesignerWindowSettingsSwitchTest
{
    /// <summary>
    /// 保存旧窗口布局时，其窗口尺寸与画布尺寸仍分别使用旧窗口的数据。
    /// </summary>
    /// <returns>断言完成时结束的任务。</returns>
    [Fact]
    public async Task SavingCurrentLayoutDuringWindowSwitchPreservesItsWindowSize()
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
            layout.Setup(x => x.LoadWindowConfigWithMetadataAsync("ScoreSurWindow", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FrontedLayoutLoadResult
                {
                    Config = new FrontedWindowConfig
                    {
                        WindowSettings = new FrontedWindowSettings { WindowWidth = 480, WindowHeight = 152 },
                        CanvasSettings = new FrontedCanvasSettings { CanvasWidth = 480, CanvasHeight = 152 }
                    },
                    Source = FrontedLayoutSource.User,
                    Path = "ScoreSurWindow.json"
                });
            FrontedWindowConfig? savedConfig = null;
            layout.Setup(x => x.SaveWindowConfigAsync(
                    "ScoreSurWindow", It.IsAny<FrontedWindowConfig>(), It.IsAny<CancellationToken>()))
                .Callback<string, FrontedWindowConfig, CancellationToken>((_, config, _) => savedConfig = config)
                .Returns(Task.CompletedTask);

            var behaviors = new Mock<IFrontedBehaviorService>();
            behaviors.Setup(x => x.LoadDocumentAsync("ScoreSurWindow", It.IsAny<CancellationToken>()))
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
            Assert.Equal(480, savedConfig.WindowSettings.WindowWidth);
            Assert.Equal(152, savedConfig.WindowSettings.WindowHeight);
            Assert.Equal(480, savedConfig.CanvasSettings.CanvasWidth);
            Assert.Equal(152, savedConfig.CanvasSettings.CanvasHeight);
        });
    }
}
