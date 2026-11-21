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
            BindingOperations.SetBinding(
                image,
                Image.SourceProperty,
                FrontedBindingFactory.Create(config.BindingPath, context.SharedDataService, context.CharacterPresentation));
            return;
        }

        if (!string.IsNullOrWhiteSpace(config.ImagePath))
        {
            var decodePixelWidth = ResolveDecodePixelWidth(config);
            image.Source = decodePixelWidth > 0 && context.ResourceResolver is FrontedResourceResolver sizedResolver
                // 静态图片在控件尺寸已知时按显示尺寸解码，避免完整解码大图后再由 WPF 降采样。
                ? sizedResolver.ResolveImage(config.ImagePath, FrontedImagePurpose.UiElement, decodePixelWidth)
                : context.ResourceResolver.ResolveImage(
                    config.ImagePath,
                    FrontedImagePurpose.UiElement);
        }
    }

    private static int ResolveDecodePixelWidth(ImageFrontedControlConfig config)
    {
        // Auto 模式按图片原始尺寸布局，没有可靠的目标尺寸；其余模式使用控件设计尺寸并保留 2 倍超采样。
        if (config.SizingMode == ImageSizingMode.Auto)
        {
            return 0;
        }

        var width = config.Width is > 0 && double.IsFinite(config.Width.Value) ? config.Width.Value : 0D;
        var height = config.Height is > 0 && double.IsFinite(config.Height.Value) ? config.Height.Value : 0D;
        var target = Math.Max(width, height) * 2D;
        if (!double.IsFinite(target) || target < 32D)
        {
            return 0;
        }

        return (int)Math.Clamp(Math.Ceiling(target), 32D, 4096D);
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
        AddLockOverlay(root, name, config, context, image.Stretch);
        AddPickingBorderOverlay(root, name, config, context, image.Stretch);
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
        AddLockOverlay(root, controlName, config, context, image.Stretch);
        AddPickingBorderOverlay(root, controlName, config, context, image.Stretch);
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
        FrontedControlBuildContext context,
        Stretch primaryStretch)
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
            Stretch = ResolveOverlayStretch(
                config.UseIndependentLockStretch,
                config.LockStretch,
                primaryStretch,
                context,
                nameof(config.LockStretch)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false
        };

        Panel.SetZIndex(overlay, config.LockZIndexOffset);
        MarkAnimationPart(overlay, controlName, config.BehaviorGuid, FrontedAnimationPartNames.LockOverlay);
        RegisterGeneratedChildName(root, overlayName, overlay);
        if (!string.IsNullOrWhiteSpace(config.LockVisibilityBindingPath))
        {
            var binding = FrontedBindingFactory.Create(config.LockVisibilityBindingPath, context.SharedDataService, context.CharacterPresentation);
            binding.Converter = new OverlayVisibilityConverter(config.LockVisibleWhen);
            BindingOperations.SetBinding(overlay, UIElement.VisibilityProperty, binding);
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
        FrontedControlBuildContext context,
        Stretch primaryStretch)
    {
        if (!config.PickingBorderAvailable)
        {
            return;
        }

        var overlayName = $"{controlName}PickingBorder";
        var overlayStretch = ResolveOverlayStretch(
            config.UseIndependentPickingBorderStretch,
            config.PickingBorderStretch,
            primaryStretch,
            context,
            nameof(config.PickingBorderStretch));

        var overlay = new Border
        {
            Name = overlayName,
            Background = CreatePickingBorderBrush(config.PickingBorderFillColor),
            OpacityMask = CreateImageBrush(ResolveOverlayImage(
                config.PickingBorderImagePath,
                DefaultPickingBorderImagePath,
                context), overlayStretch),
            Visibility = Visibility.Hidden,
            Opacity = 0,
            IsHitTestVisible = false
        };

        Panel.SetZIndex(overlay, config.PickingBorderZIndexOffset);
        MarkAnimationPart(overlay, controlName, config.BehaviorGuid, FrontedAnimationPartNames.PickingBorder);
        RegisterGeneratedChildName(root, overlayName, overlay);
        root.Children.Add(overlay);
    }

    private static Brush CreatePickingBorderBrush(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return Brushes.White;
        }

        try
        {
            return new BrushConverter().ConvertFromString(color) as Brush ?? Brushes.White;
        }
        catch (FormatException)
        {
            return Brushes.White;
        }
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

    private static ImageBrush? CreateImageBrush(ImageSource? imageSource, Stretch stretch)
    {
        if (imageSource is null)
        {
            return null;
        }

        var brush = new ImageBrush(imageSource) { Stretch = stretch };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
        if (brush.CanFreeze)
        {
            // 锁图/选择边框等 overlay 画刷在控件生命周期内不变，冻结后减少变更通知开销。
            brush.Freeze();
        }

        return brush;
    }

    private static Stretch ResolveOverlayStretch(
        bool useIndependentStretch,
        string? independentStretch,
        Stretch primaryStretch,
        FrontedControlBuildContext context,
        string propertyName)
    {
        if (!useIndependentStretch)
        {
            return primaryStretch;
        }

        var resolvedStretch = Stretch.Fill;
        FrontedControlFactoryHelper.TryApplyEnum<Stretch>(
            independentStretch,
            value => resolvedStretch = value,
            context,
            propertyName);
        return resolvedStretch;
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
