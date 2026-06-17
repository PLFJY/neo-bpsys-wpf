using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedRendererBehaviorGuidTest
{
    [Fact]
    public void Renderer_SetsBehaviorGuidAttachedProperty()
    {
        RunOnStaThread(() =>
        {
            var guid = Guid.NewGuid();
            var renderer = new FrontedRenderer(
                new ServiceCollection().BuildServiceProvider(),
                Mock.Of<ISharedDataService>(),
                Mock.Of<IFrontedResourceResolver>(),
                new FrontedControlRegistry([new RecordingControl()]),
                NullLogger<FrontedRenderer>.Instance);
            var canvas = new Canvas();
            var config = new FrontedCanvasConfig
            {
                CanvasWidth = 100,
                CanvasHeight = 100,
                Controls =
                {
                    ["Target"] = new RecordingConfig
                    {
                        BehaviorGuid = guid,
                        Width = 20,
                        Height = 20
                    }
                }
            };

            renderer.RenderToCanvas(canvas, config, new FrontedRenderContext
            {
                WindowId = "TestWindow",
                CanvasName = "BaseCanvas",
                IsDesignerPreview = true
            });

            var element = Assert.IsType<Border>(Assert.Single(canvas.Children));
            Assert.Equal(guid, FrontedRendererProperties.GetBehaviorGuid(element));
        });
    }

    [Fact]
    public void Resolver_ResolvesGeneratedPickingBorderAnimationPart()
    {
        RunOnStaThread(() =>
        {
            var guid = Guid.NewGuid();
            var canvas = new Canvas();
            var host = new Border
            {
                Name = "Arms_Factory",
                Width = 100,
                Height = 100
            };
            var pickingBorder = new Border
            {
                Width = 100,
                Height = 100
            };

            FrontedRendererProperties.SetIsGeneratedControl(host, true);
            FrontedRendererProperties.SetBehaviorGuid(host, guid);
            FrontedRendererProperties.SetRegisteredName(host, "Arms_Factory");
            FrontedRendererProperties.SetIsGeneratedControl(pickingBorder, true);
            FrontedRendererProperties.SetIsAnimationAuxiliaryElement(pickingBorder, true);
            FrontedRendererProperties.SetParentBehaviorGuid(pickingBorder, guid);
            FrontedRendererProperties.SetParentRegisteredName(pickingBorder, "Arms_Factory");
            FrontedRendererProperties.SetAnimationPartName(pickingBorder, FrontedAnimationPartNames.PickingBorder);
            FrontedRendererProperties.SetRegisteredName(pickingBorder, "Arms_Factory__PickingBorder");

            host.Child = pickingBorder;
            canvas.Children.Add(host);
            canvas.Measure(new Size(200, 200));
            canvas.Arrange(new Rect(0, 0, 200, 200));
            canvas.UpdateLayout();

            var target = new FrontedAnimationTargetResolver().Resolve(
                FrontedAnimationTargetReference.Parse($"part:{guid}:PickingBorder"),
                new FrontedAnimationExecutionContext
                {
                    Root = canvas,
                    SelfBehaviorGuid = guid,
                    SelfDisplayName = "Arms_Factory"
                });

            Assert.NotNull(target);
            Assert.Equal(guid, target!.BehaviorGuid);
            Assert.Equal(FrontedAnimationPartNames.PickingBorder, FrontedRendererProperties.GetAnimationPartName(target.Element));
        });
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }

    private sealed class RecordingConfig : FrontedControlConfigBase
    {
        public RecordingConfig()
        {
            ControlType = "Recording";
        }
    }

    private sealed class RecordingControl : IFrontedControl
    {
        public string ControlType => "Recording";
        public Type ConfigType => typeof(RecordingConfig);

        public FrameworkElement Create(string name, FrontedControlConfigBase config, FrontedControlBuildContext context) =>
            new Border
            {
                Name = name,
                Width = config.Width ?? 0,
                Height = config.Height ?? 0
            };
    }
}
