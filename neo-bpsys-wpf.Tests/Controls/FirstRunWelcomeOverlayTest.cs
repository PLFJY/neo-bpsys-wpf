#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.ProductTour.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

/// <summary>
/// Tests first-run welcome overlay styling boundaries.
/// </summary>
public sealed class FirstRunWelcomeOverlayTest
{
    [Fact]
    public void WelcomeCardDoesNotLocallyOverrideStyleAppearance()
    {
        WpfTestThread.Run(() =>
        {
            var overlay = new FirstRunWelcomeOverlay();

            var card = FindByName<Border>(overlay, "WelcomeCard");

            Assert.NotNull(card);
            Assert.Equal(DependencyProperty.UnsetValue, card.ReadLocalValue(Border.BackgroundProperty));
            Assert.Equal(DependencyProperty.UnsetValue, card.ReadLocalValue(Border.BorderThicknessProperty));
            Assert.Equal(DependencyProperty.UnsetValue, card.ReadLocalValue(UIElement.EffectProperty));
        });
    }

    private static T? FindByName<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        if (root is T element && element.Name == name)
        {
            return element;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var result = FindByName<T>(VisualTreeHelper.GetChild(root, i), name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
