using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 行为拥有的生成动画部件的默认渲染器。
/// </summary>
public sealed class FrontedBehaviorAnimationPartRenderer(
    IFrontedResourceResolver resourceResolver) : IFrontedBehaviorAnimationPartRenderer
{
    /// <inheritdoc />
    public void ApplyAnimationParts(FrameworkElement root, FrontedBehaviorDocument behaviorDocument)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(behaviorDocument);

        foreach (var set in behaviorDocument.ControlBehaviorSets)
        {
            if (set.BehaviorGuid == Guid.Empty || set.AnimationParts.Count == 0)
            {
                continue;
            }

            var parent = EnumerateFrameworkElements(root)
                .FirstOrDefault(item => FrontedRendererProperties.GetIsGeneratedControl(item)
                                        && !FrontedRendererProperties.GetIsAnimationAuxiliaryElement(item)
                                        && FrontedRendererProperties.GetBehaviorGuid(item) == set.BehaviorGuid);
            if (parent is null)
            {
                continue;
            }

            var host = EnsureHost(parent, set.DisplayName);
            ApplyParts(root, host, set);
        }
    }

    private static Canvas CreateLayer() =>
        new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false
        };

    private void ApplyParts(FrameworkElement root, Grid host, ControlBehaviorSet set)
    {
        var below = GetOrCreateLayer(host, "PART_BehaviorAnimationBelow", insertIndex: 0);
        var above = GetOrCreateLayer(host, "PART_BehaviorAnimationAbove", insertIndex: host.Children.Count);
        below.Children.Clear();
        above.Children.Clear();

        foreach (var part in set.AnimationParts)
        {
            if (string.IsNullOrWhiteSpace(part.Name))
            {
                continue;
            }

            var element = CreateElement(part);
            ApplyLayout(element, part, host);
            var partEffectHost = FrontedEffectHostFactory.Wrap(element);
            ApplyEffect(partEffectHost, element, part.Effect);
            MarkPart(element, host, set.DisplayName, set.BehaviorGuid, part.Name);
            RegisterGeneratedName(root, $"{set.DisplayName}__{part.Name}", element);
            (part.Layer == FrontedAnimationPartLayer.BelowContent ? below : above).Children.Add(partEffectHost);
        }
    }

    private FrameworkElement CreateElement(FrontedAnimationPartConfig config)
    {
        return config.Kind switch
        {
            FrontedAnimationPartKind.Border => new Border
            {
                Background = ParseBrush(config.Fill),
                BorderBrush = ParseBrush(config.Stroke),
                BorderThickness = new Thickness(Math.Max(0D, config.StrokeThickness))
            },
            FrontedAnimationPartKind.Image => new Image
            {
                Source = resourceResolver.ResolveImage(config.ImagePath, FrontedImagePurpose.UiElement),
                Stretch = Stretch.Fill
            },
            _ => new Rectangle
            {
                Fill = ParseBrush(config.Fill),
                Stroke = ParseBrush(config.Stroke),
                StrokeThickness = Math.Max(0D, config.StrokeThickness)
            }
        };
    }

    private static void ApplyLayout(
        FrameworkElement element,
        FrontedAnimationPartConfig config,
        FrameworkElement parent)
    {
        Canvas.SetLeft(element, config.Left);
        Canvas.SetTop(element, config.Top);
        Panel.SetZIndex(element, config.ZIndex);
        element.Opacity = Math.Clamp(config.Opacity, 0D, 1D);
        element.Visibility = Enum.TryParse<Visibility>(config.Visibility, true, out var visibility)
            ? visibility
            : Visibility.Hidden;
        element.IsHitTestVisible = config.IsHitTestVisible;

        void UpdateSize()
        {
            element.Width = ResolveLength(config.WidthText, config.Width, parent.ActualWidth, element.Width);
            element.Height = ResolveLength(config.HeightText, config.Height, parent.ActualHeight, element.Height);
        }

        parent.Loaded += (_, _) => UpdateSize();
        parent.SizeChanged += (_, _) => UpdateSize();
        UpdateSize();
    }

    private static double ResolveLength(string? text, double? pixels, double parentSize, double fallback)
    {
        if (!string.IsNullOrWhiteSpace(text)
            && text.Trim().EndsWith('%')
            && double.TryParse(
                text.Trim()[..^1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var percentage))
        {
            return Math.Max(0D, parentSize * percentage / 100D);
        }

        if (!string.IsNullOrWhiteSpace(text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var textPixels))
        {
            return Math.Max(0D, textPixels);
        }

        return pixels is { } fixedSize && double.IsFinite(fixedSize)
            ? Math.Max(0D, fixedSize)
            : fallback;
    }

    private static Brush? ParseBrush(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return (Brush?)new BrushConverter().ConvertFromString(value);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static void ApplyEffect(FrontedEffectHost partEffectHost, FrameworkElement element, FrontedVisualEffectConfig? config)
    {
        if (config is null || config.Kind == FrontedVisualEffectKind.None)
        {
            partEffectHost.Effect = null;
            return;
        }

        // Glow and drop shadow deliberately remain on the semantic part child.
        partEffectHost.Effect = null;

        var color = Colors.Transparent;
        if (!string.IsNullOrWhiteSpace(config.Color))
        {
            try
            {
                color = (Color)ColorConverter.ConvertFromString(config.Color)!;
            }
            catch (FormatException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        element.Effect = new DropShadowEffect
        {
            Color = color,
            Opacity = Math.Clamp(config.Opacity, 0D, 1D),
            BlurRadius = NormalizeRadius(config.BlurRadius),
            ShadowDepth = config.Kind == FrontedVisualEffectKind.Glow ? 0D : Math.Max(0D, config.ShadowDepth),
            Direction = config.Direction
        };
    }

    private static double NormalizeRadius(double value) => double.IsFinite(value) ? Math.Max(0D, value) : 0D;

    private static void MarkPart(
        FrameworkElement element,
        FrameworkElement parent,
        string parentName,
        Guid parentGuid,
        string partName)
    {
        FrontedRendererProperties.SetIsGeneratedControl(element, true);
        FrontedRendererProperties.SetIsAnimationAuxiliaryElement(element, true);
        FrontedRendererProperties.SetParentBehaviorGuid(element, parentGuid);
        FrontedRendererProperties.SetParentRegisteredName(element, parentName);
        FrontedRendererProperties.SetAnimationPartName(element, partName);
        FrontedRendererProperties.SetAnimationPartParent(element, parent);
        FrontedRendererProperties.SetRegisteredName(element, $"{parentName}__{partName}");
    }

    private static Grid EnsureHost(FrameworkElement parent, string displayName)
    {
        var effectHost = FrontedEffectHostFactory.FindEffectHost(parent)
                         ?? FrontedEffectHostFactory.Wrap(parent);
        if (VisualTreeHelper.GetParent(effectHost) is Grid existing
            && existing.Children.Contains(effectHost)
            && existing.Children.OfType<Canvas>().Any(item => item.Name == "PART_BehaviorAnimationAbove"))
        {
            return existing;
        }

        if (VisualTreeHelper.GetParent(effectHost) is not Panel owner)
        {
            return new Grid();
        }

        var index = owner.Children.IndexOf(effectHost);
        if (index < 0)
        {
            return new Grid();
        }

        var host = new Grid
        {
            ClipToBounds = false
        };

        FrontedEffectHostFactory.TransferAttachedLayout(effectHost, host);

        var below = CreateLayer();
        below.Name = "PART_BehaviorAnimationBelow";
        var above = CreateLayer();
        above.Name = "PART_BehaviorAnimationAbove";

        owner.Children.RemoveAt(index);
        host.Children.Add(below);
        host.Children.Add(effectHost);
        host.Children.Add(above);
        owner.Children.Insert(index, host);
        return host;
    }

    private static Canvas GetOrCreateLayer(Grid host, string name, int insertIndex)
    {
        var layer = host.Children.OfType<Canvas>().FirstOrDefault(item => item.Name == name);
        if (layer is not null)
        {
            return layer;
        }

        layer = CreateLayer();
        layer.Name = name;
        host.Children.Insert(Math.Clamp(insertIndex, 0, host.Children.Count), layer);
        return layer;
    }

    private static IEnumerable<FrameworkElement> EnumerateFrameworkElements(DependencyObject root)
    {
        if (root is FrameworkElement frameworkElement)
        {
            yield return frameworkElement;
        }

        var children = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < children; i++)
        {
            foreach (var child in EnumerateFrameworkElements(VisualTreeHelper.GetChild(root, i)))
            {
                yield return child;
            }
        }
    }

    private static void RegisterGeneratedName(FrameworkElement root, string name, FrameworkElement element)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var nameScopeOwner = (FrameworkElement?)Window.GetWindow(root) ?? root;
        if (NameScope.GetNameScope(nameScopeOwner) is null)
        {
            NameScope.SetNameScope(nameScopeOwner, new NameScope());
        }

        var nameScope = NameScope.GetNameScope(nameScopeOwner);
        try
        {
            nameScope?.UnregisterName(name);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        nameScopeOwner.RegisterName(name, element);
    }

}
