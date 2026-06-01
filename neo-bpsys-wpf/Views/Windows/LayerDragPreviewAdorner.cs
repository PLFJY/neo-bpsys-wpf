using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Semi-transparent layer-panel drag preview that follows the pointer during reorder.
/// </summary>
internal sealed class LayerDragPreviewAdorner : Adorner
{
    private const double PointerOffsetX = 12D;
    private const double PointerOffsetY = 8D;

    private readonly Border _card;
    private Point _position;

    public LayerDragPreviewAdorner(UIElement adornedElement, FrontedControlDesignItem item)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
        Opacity = 0.75;

        var nameBlock = new TextBlock
        {
            Text = item.Name,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var detailsPanel = new StackPanel { Orientation = Orientation.Horizontal };
        detailsPanel.Children.Add(new TextBlock
        {
            FontSize = 12,
            Opacity = 0.78,
            Text = GetControlTypeDisplay(item.Config.ControlType)
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Margin = new Thickness(10, 0, 0, 0),
            FontSize = 12,
            Opacity = 0.78,
            Text = I18nHelper.GetLocalizedString("ZIndexShort")
        });
        detailsPanel.Children.Add(new TextBlock
        {
            Margin = new Thickness(3, 0, 0, 0),
            FontSize = 12,
            Opacity = 0.78,
            Text = item.Config.ZIndex.ToString()
        });

        _card = new Border
        {
            MinWidth = 140,
            MaxWidth = 280,
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Background = TryGetThemeBrush("CardBackgroundFillColorDefaultBrush", Brushes.White),
            BorderBrush = TryGetThemeBrush("AccentTextFillColorPrimaryBrush", Brushes.DeepSkyBlue),
            Child = new StackPanel
            {
                Children =
                {
                    nameBlock,
                    detailsPanel
                }
            }
        };

        AddVisualChild(_card);
    }

    public void SetPosition(Point positionRelativeToAdornedElement)
    {
        _position = new Point(
            positionRelativeToAdornedElement.X + PointerOffsetX,
            positionRelativeToAdornedElement.Y + PointerOffsetY);
        InvalidateArrange();
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _card;

    protected override Size MeasureOverride(Size constraint)
    {
        _card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return _card.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _card.Arrange(new Rect(_position, _card.DesiredSize));
        return finalSize;
    }

    private static Brush TryGetThemeBrush(string resourceKey, Brush fallback) =>
        Application.Current?.TryFindResource(resourceKey) as Brush ?? fallback;

    private static string GetControlTypeDisplay(string? controlType)
    {
        if (string.IsNullOrWhiteSpace(controlType))
        {
            return string.Empty;
        }

        var key = $"Designer.ControlType.{controlType}";
        var localized = I18nHelper.GetLocalizedString(key);
        return string.Equals(localized, key, StringComparison.Ordinal) ? controlType : localized;
    }
}
