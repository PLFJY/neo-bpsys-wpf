using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
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

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        exception?.Throw();
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
