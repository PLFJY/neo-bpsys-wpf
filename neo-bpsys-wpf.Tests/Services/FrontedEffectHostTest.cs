using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedEffectHostTest
{
    [Fact]
    public void Wrap_TransfersOnlyPanelAttachedLayoutAndPreservesSemanticIdentity()
    {
        WpfTestThread.Run(() =>
        {
            var element = new Border
            {
                Name = "SemanticRoot",
                Width = 120,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Effect = new DropShadowEffect()
            };
            Canvas.SetLeft(element, 12);
            Canvas.SetTop(element, 24);
            Canvas.SetRight(element, 36);
            Canvas.SetBottom(element, 48);
            Panel.SetZIndex(element, 9);
            FrontedRendererProperties.SetIsGeneratedControl(element, true);

            var host = FrontedEffectHostFactory.Wrap(element);

            Assert.Same(element, host.HostedElement);
            Assert.Equal(120, element.Width);
            Assert.Equal(48, element.Height);
            Assert.Equal(HorizontalAlignment.Right, element.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Bottom, element.VerticalAlignment);
            Assert.IsType<DropShadowEffect>(element.Effect);
            Assert.Null(host.Effect);
            Assert.Equal(12, Canvas.GetLeft(host));
            Assert.Equal(24, Canvas.GetTop(host));
            Assert.Equal(36, Canvas.GetRight(host));
            Assert.Equal(48, Canvas.GetBottom(host));
            Assert.Equal(9, Panel.GetZIndex(host));
            Assert.True(double.IsNaN(Canvas.GetLeft(element)));
            Assert.True(double.IsNaN(Canvas.GetTop(element)));
            Assert.True(double.IsNaN(Canvas.GetRight(element)));
            Assert.True(double.IsNaN(Canvas.GetBottom(element)));
            Assert.Equal(0, Panel.GetZIndex(element));
            Assert.True(FrontedRendererProperties.GetIsGeneratedControl(element));
            Assert.False(FrontedRendererProperties.GetIsGeneratedControl(host));
            Assert.Same(host, FrontedEffectHostFactory.Wrap(host));
            Assert.Same(host, FrontedEffectHostFactory.Wrap(element));
        });
    }

    [Fact]
    public void Host_MeasuresHostedElementWithoutChangingItsSizeProperties()
    {
        WpfTestThread.Run(() =>
        {
            var element = new Border { Width = 120, Height = 48, MinWidth = 20, MaxHeight = 60 };
            var host = FrontedEffectHostFactory.Wrap(element);

            host.Measure(new Size(300, 300));
            host.Arrange(new Rect(0, 0, 120, 48));

            Assert.Equal(element.DesiredSize, host.DesiredSize);
            Assert.Equal(120, element.Width);
            Assert.Equal(48, element.Height);
            Assert.Equal(20, element.MinWidth);
            Assert.Equal(60, element.MaxHeight);
        });
    }
}
