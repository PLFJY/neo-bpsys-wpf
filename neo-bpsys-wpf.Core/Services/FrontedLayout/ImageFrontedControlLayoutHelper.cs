using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Markup;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

internal static class ImageFrontedControlLayoutHelper
{
    private const string DefaultLockImagePath = "Resources/CurrentBanLock.png";
    private const string DefaultPickingBorderImagePath = "Resources/pickingBorder.png";

    public static void ApplyImageLayout(
        Image image,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context)
    {
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        ApplyStretch(image, config, context);

        switch (config.SizingMode)
        {
            case ImageSizingMode.FillContainer:
                ApplyHorizontalAlignment(image, config, context, HorizontalAlignment.Stretch);
                ApplyVerticalAlignment(image, config, context, VerticalAlignment.Stretch);
                break;
            case ImageSizingMode.OverflowCrop:
                ApplyHorizontalAlignment(image, config, context, HorizontalAlignment.Center);
                ApplyVerticalAlignment(image, config, context, VerticalAlignment.Center);
                break;
            case ImageSizingMode.Auto:
            default:
                ApplyHorizontalAlignment(image, config, context);
                ApplyVerticalAlignment(image, config, context);
                break;
        }
    }

    public static void ApplyImageSource(
        Image image,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context)
    {
        if (!string.IsNullOrWhiteSpace(config.BindingPath))
        {
            BindingOperations.SetBinding(image, Image.SourceProperty, new Binding(config.BindingPath)
            {
                Source = context.SharedDataService
            });
            return;
        }

        if (!string.IsNullOrWhiteSpace(config.ImagePath))
        {
            image.Source = context.ResourceResolver.ResolveImage(
                config.ImagePath,
                FrontedImagePurpose.UiElement);
        }
    }

    public static Grid CreateImageLayerRoot(
        string name,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context,
        Image image)
    {
        var root = new Grid { Name = name };
        root.ClipToBounds = config.ClipToBounds;
        if (config.CornerRadius is > 0)
        {
            ApplyCornerRadiusClip(root, config.CornerRadius);
        }

        root.Children.Add(CreatePrimaryContentHost(config, image));
        AddLockOverlay(root, name, config, context);
        AddPickingBorderOverlay(root, name, config, context);
        return root;
    }

    public static Grid CreateBorderedImageContent(
        string controlName,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context,
        Image image)
    {
        var root = new Grid();
        root.Children.Add(CreatePrimaryContentHost(config, image));
        AddLockOverlay(root, controlName, config, context);
        AddPickingBorderOverlay(root, controlName, config, context);
        return root;
    }

    private static Grid CreatePrimaryContentHost(
        ImageFrontedControlConfig config,
        Image image)
    {
        var host = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = false,
            IsHitTestVisible = false
        };
        FrontedRendererProperties.SetIsPrimaryContentElement(host, true);
        FrontedRendererProperties.SetParentBehaviorGuid(host, config.BehaviorGuid);
        host.Children.Add(image);
        return host;
    }

    public static void ApplyCornerRadiusClip(FrameworkElement element, double? cornerRadius)
    {
        if (!cornerRadius.HasValue || cornerRadius.Value <= 0)
        {
            return;
        }

        var radius = cornerRadius.Value;

        void UpdateClip()
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                return;
            }

            element.Clip = new RectangleGeometry(
                new Rect(0, 0, element.ActualWidth, element.ActualHeight),
                radius,
                radius);
        }

        element.Loaded += (_, _) => UpdateClip();
        element.SizeChanged += (_, _) => UpdateClip();
    }

    private static void ApplyStretch(
        Image image,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context)
    {
        FrontedControlFactoryHelper.TryApplyEnum<Stretch>(
            config.Stretch,
            value => image.Stretch = value,
            context,
            nameof(config.Stretch));
    }

    private static void AddLockOverlay(
        Grid root,
        string controlName,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context)
    {
        if (!config.Lockable)
        {
            return;
        }

        var overlayName = $"{controlName}{FrontedAnimationPartNames.LockOverlay}";
        var overlay = new Image
        {
            Name = overlayName,
            Source = ResolveOverlayImage(config.LockImagePath, DefaultLockImagePath, context),
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false
        };

        Panel.SetZIndex(overlay, config.LockZIndexOffset);
        MarkAnimationPart(overlay, controlName, config.BehaviorGuid, FrontedAnimationPartNames.LockOverlay);
        RegisterGeneratedChildName(root, overlayName, overlay);
        if (!string.IsNullOrWhiteSpace(config.LockVisibilityBindingPath))
        {
            BindingOperations.SetBinding(overlay, UIElement.VisibilityProperty, new Binding(config.LockVisibilityBindingPath)
            {
                Source = context.SharedDataService,
                Converter = new OverlayVisibilityConverter(config.LockVisibleWhen)
            });
        }
        else
        {
            overlay.Visibility = config.LockVisibleWhen == FrontedOverlayVisibilityMode.Always
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        root.Children.Add(overlay);
    }

    private static void AddPickingBorderOverlay(
        Grid root,
        string controlName,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context)
    {
        if (!config.PickingBorderAvailable)
        {
            return;
        }

        var overlayName = string.IsNullOrWhiteSpace(config.PickingBorderName)
            ? $"{controlName}PickingBorder"
            : config.PickingBorderName;

        var overlay = new Border
        {
            Name = overlayName,
            Background = Brushes.White,
            OpacityMask = CreateImageBrush(ResolveOverlayImage(
                config.PickingBorderImagePath,
                DefaultPickingBorderImagePath,
                context)),
            Visibility = Visibility.Hidden,
            Opacity = 0,
            IsHitTestVisible = false
        };

        Panel.SetZIndex(overlay, config.PickingBorderZIndexOffset);
        MarkAnimationPart(overlay, controlName, config.BehaviorGuid, FrontedAnimationPartNames.PickingBorder);
        RegisterGeneratedChildName(root, overlayName, overlay);
        root.Children.Add(overlay);
    }

    private static void MarkAnimationPart(
        FrameworkElement element,
        string parentRegisteredName,
        Guid parentBehaviorGuid,
        string partName)
    {
        FrontedRendererProperties.SetIsGeneratedControl(element, true);
        FrontedRendererProperties.SetIsAnimationAuxiliaryElement(element, true);
        FrontedRendererProperties.SetParentBehaviorGuid(element, parentBehaviorGuid);
        FrontedRendererProperties.SetParentRegisteredName(element, parentRegisteredName);
        FrontedRendererProperties.SetAnimationPartName(element, partName);
        FrontedRendererProperties.SetRegisteredName(element, element.Name);
    }

    private static ImageSource? ResolveOverlayImage(
        string? imagePath,
        string fallbackPath,
        FrontedControlBuildContext context)
    {
        var source = !string.IsNullOrWhiteSpace(imagePath)
            ? context.ResourceResolver.ResolveImage(imagePath, FrontedImagePurpose.UiElement)
            : null;

        return source ?? context.ResourceResolver.ResolveImage(fallbackPath, FrontedImagePurpose.UiElement);
    }

    private static ImageBrush? CreateImageBrush(ImageSource? imageSource)
    {
        if (imageSource is null)
        {
            return null;
        }

        var brush = new ImageBrush(imageSource) { Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
        return brush;
    }

    private static void RegisterGeneratedChildName(FrameworkElement root, string name, FrameworkElement child)
    {
        FrameworkElement? registeredOwner = null;

        root.Loaded += (_, _) =>
        {
            var owner = (FrameworkElement?)Window.GetWindow(root) ?? root;
            var nameScope = NameScope.GetNameScope(owner);
            if (nameScope is null)
            {
                nameScope = new NameScope();
                NameScope.SetNameScope(owner, nameScope);
            }

            TryUnregisterName(nameScope, name);
            nameScope.RegisterName(name, child);
            registeredOwner = owner;
        };

        root.Unloaded += (_, _) =>
        {
            if (registeredOwner is null)
            {
                return;
            }

            var nameScope = NameScope.GetNameScope(registeredOwner);
            TryUnregisterName(nameScope, name);
            registeredOwner = null;
        };
    }

    private static void TryUnregisterName(INameScope? nameScope, string name)
    {
        if (nameScope is null)
        {
            return;
        }

        try
        {
            nameScope.UnregisterName(name);
        }
        catch (ArgumentException)
        {
            // Name was not registered in this namescope.
        }
        catch (InvalidOperationException)
        {
            // The WPF owner may already have lost its namescope during unload.
        }
    }

    private sealed class OverlayVisibilityConverter : IValueConverter
    {
        private readonly FrontedOverlayVisibilityMode _mode;

        public OverlayVisibilityConverter(FrontedOverlayVisibilityMode mode)
        {
            _mode = mode;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (_mode == FrontedOverlayVisibilityMode.Always)
            {
                return Visibility.Visible;
            }

            var boolValue = value is bool b && b;
            var visible = _mode == FrontedOverlayVisibilityMode.VisibleWhenTrue
                ? boolValue
                : !boolValue;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    private static void ApplyHorizontalAlignment(
        Image image,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context,
        HorizontalAlignment? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(config.HorizontalAlignment) && defaultValue is not null)
        {
            image.HorizontalAlignment = defaultValue.Value;
            return;
        }

        FrontedControlFactoryHelper.TryApplyEnum<HorizontalAlignment>(
            config.HorizontalAlignment,
            value => image.HorizontalAlignment = value,
            context,
            nameof(config.HorizontalAlignment));
    }

    private static void ApplyVerticalAlignment(
        Image image,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context,
        VerticalAlignment? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(config.VerticalAlignment) && defaultValue is not null)
        {
            image.VerticalAlignment = defaultValue.Value;
            return;
        }

        FrontedControlFactoryHelper.TryApplyEnum<VerticalAlignment>(
            config.VerticalAlignment,
            value => image.VerticalAlignment = value,
            context,
            nameof(config.VerticalAlignment));
    }
}
