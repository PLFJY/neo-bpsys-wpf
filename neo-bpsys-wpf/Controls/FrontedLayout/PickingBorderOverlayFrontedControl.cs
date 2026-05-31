using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 BP 选择呼吸边框覆盖控件工厂。
/// </summary>
public class PickingBorderOverlayFrontedControl(
    ILogger<PickingBorderOverlayFrontedControl>? logger = null) : IFrontedControl
{
    private readonly ILogger<PickingBorderOverlayFrontedControl>? _logger = logger;

    /// <inheritdoc />
    public string ControlType => "PickingBorderOverlay";

    /// <inheritdoc />
    public Type ConfigType => typeof(PickingBorderOverlayControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not PickingBorderOverlayControlConfig overlayConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a PickingBorderOverlay config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        return new PickingBorderOverlayElement(
            name,
            overlayConfig,
            settingsHostService,
            context.ResourceResolver,
            _logger ?? context.Logger);
    }

    private sealed class PickingBorderOverlayElement : Border
    {
        private readonly PickingBorderOverlayControlConfig _config;
        private readonly ISettingsHostService _settingsHostService;
        private readonly IFrontedResourceResolver _resourceResolver;
        private readonly ILogger? _logger;
        private Settings? _subscribedSettings;
        private BpWindowSettings? _subscribedBpWindowSettings;
        private bool _isSubscribed;

        public PickingBorderOverlayElement(
            string name,
            PickingBorderOverlayControlConfig config,
            ISettingsHostService settingsHostService,
            IFrontedResourceResolver resourceResolver,
            ILogger? logger)
        {
            _config = config;
            _settingsHostService = settingsHostService;
            _resourceResolver = resourceResolver;
            _logger = logger;

            Name = name;
            Canvas.SetLeft(this, config.Left);
            Canvas.SetTop(this, config.Top);
            Panel.SetZIndex(this, config.ZIndex);

            if (config.Width.HasValue)
            {
                Width = config.Width.Value;
            }

            if (config.Height.HasValue)
            {
                Height = config.Height.Value;
            }

            Visibility = config.InitiallyHidden ? Visibility.Hidden : Visibility.Visible;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isSubscribed)
            {
                return;
            }

            _isSubscribed = true;
            _settingsHostService.SettingsChanged += OnSettingsChanged;
            SubscribeSettings(_settingsHostService.Settings);
            UpdateVisuals();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isSubscribed)
            {
                return;
            }

            _isSubscribed = false;
            _settingsHostService.SettingsChanged -= OnSettingsChanged;
            SubscribeSettings(null);
        }

        private void OnSettingsChanged(object? sender, Settings settings)
        {
            SubscribeSettings(settings);
            UpdateVisuals();
        }

        private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(Settings.BpWindowSettings))
            {
                SubscribeBpWindowSettings(_subscribedSettings?.BpWindowSettings);
                UpdateVisuals();
            }
        }

        private void OnBpWindowSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(BpWindowSettings.PickingBorderImage)
                || e.PropertyName == nameof(BpWindowSettings.PickingBorderImageUri)
                || e.PropertyName == nameof(BpWindowSettings.PickingBorderBrush)
                || e.PropertyName == nameof(BpWindowSettings.PickingBorderColor))
            {
                UpdateVisuals();
            }
        }

        private void SubscribeSettings(Settings? settings)
        {
            if (_subscribedSettings == settings)
            {
                return;
            }

            if (_subscribedSettings != null)
            {
                _subscribedSettings.PropertyChanged -= OnSettingsPropertyChanged;
            }

            _subscribedSettings = settings;

            if (_subscribedSettings != null)
            {
                _subscribedSettings.PropertyChanged += OnSettingsPropertyChanged;
            }

            SubscribeBpWindowSettings(_subscribedSettings?.BpWindowSettings);
        }

        private void SubscribeBpWindowSettings(BpWindowSettings? settings)
        {
            if (_subscribedBpWindowSettings == settings)
            {
                return;
            }

            if (_subscribedBpWindowSettings != null)
            {
                _subscribedBpWindowSettings.PropertyChanged -= OnBpWindowSettingsPropertyChanged;
            }

            _subscribedBpWindowSettings = settings;

            if (_subscribedBpWindowSettings != null)
            {
                _subscribedBpWindowSettings.PropertyChanged += OnBpWindowSettingsPropertyChanged;
            }
        }

        private void UpdateVisuals()
        {
            Background = ResolveFill();

            var imageSource = !string.IsNullOrWhiteSpace(_config.BorderImagePath)
                ? _resourceResolver.ResolveImage(_config.BorderImagePath)
                : _settingsHostService.Settings.BpWindowSettings.PickingBorderImage;

            OpacityMask = imageSource is null
                ? null
                : new ImageBrush(imageSource) { Stretch = Stretch.Fill };
        }

        private Brush ResolveFill()
        {
            if (string.IsNullOrWhiteSpace(_config.FillColor))
            {
                return _settingsHostService.Settings.BpWindowSettings.PickingBorderBrush;
            }

            try
            {
                return (Brush)new BrushConverter().ConvertFromString(_config.FillColor)!;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Invalid PickingBorderOverlay FillColor. Control: {TargetControlName}, FillColor: {FillColor}",
                    _config.TargetControlName,
                    _config.FillColor);
                return _settingsHostService.Settings.BpWindowSettings.PickingBorderBrush;
            }
        }
    }
}
