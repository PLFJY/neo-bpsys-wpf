using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 当前局/全局 Ban 位显示控件工厂。
/// </summary>
public class BanSlotDisplayFrontedControl(ILogger<BanSlotDisplayFrontedControl>? logger = null) : IFrontedControl
{
    private readonly ILogger<BanSlotDisplayFrontedControl>? _logger = logger;

    /// <inheritdoc />
    public string ControlType => "BanSlotDisplay";

    /// <inheritdoc />
    public Type ConfigType => typeof(BanSlotDisplayControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not BanSlotDisplayControlConfig banConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a BanSlotDisplay config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        return new BanSlotDisplayElement(
            name,
            banConfig,
            context.SharedDataService,
            settingsHostService,
            context.ResourceResolver,
            _logger ?? context.Logger);
    }

    private sealed class BanSlotDisplayElement : Border
    {
        private static readonly BooleanToReverseVisibilityConverter ReverseVisibilityConverter = new();

        private readonly BanSlotDisplayControlConfig _config;
        private readonly ISettingsHostService _settingsHostService;
        private readonly IFrontedResourceResolver _resourceResolver;
        private readonly Image? _lockImage;
        private Settings? _subscribedSettings;
        private BpWindowSettings? _subscribedBpWindowSettings;
        private bool _isSubscribed;

        public BanSlotDisplayElement(
            string name,
            BanSlotDisplayControlConfig config,
            ISharedDataService sharedDataService,
            ISettingsHostService settingsHostService,
            IFrontedResourceResolver resourceResolver,
            ILogger? logger)
        {
            _config = config;
            _settingsHostService = settingsHostService;
            _resourceResolver = resourceResolver;

            var outer = CutSceneFrontedControlHelper.CreateOuterBorder(name, config);
            Name = outer.Name;
            Width = outer.Width;
            Height = outer.Height;
            Canvas.SetLeft(this, Canvas.GetLeft(outer));
            Canvas.SetTop(this, Canvas.GetTop(outer));
            Panel.SetZIndex(this, Panel.GetZIndex(outer));

            var grid = new Grid();
            var banImage = new Image();
            ApplyImageLayout(banImage, config, logger);

            var hasValidIndex = HasValidIndex(sharedDataService, config, logger);
            if (hasValidIndex)
            {
                BindingOperations.SetBinding(banImage, Image.SourceProperty, new Binding(GetBanImageBindingPath(config))
                {
                    Source = sharedDataService
                });
            }

            grid.Children.Add(banImage);

            if (config.ShowLockOverlay && hasValidIndex)
            {
                _lockImage = new Image();
                ApplyImageLayout(_lockImage, config, logger);
                BindingOperations.SetBinding(_lockImage, VisibilityProperty, new Binding(GetCanBanBindingPath(config))
                {
                    Source = sharedDataService,
                    Converter = ReverseVisibilityConverter
                });
                Panel.SetZIndex(_lockImage, config.LockZIndexOffset);
                grid.Children.Add(_lockImage);

                Loaded += OnLoaded;
                Unloaded += OnUnloaded;
            }

            Child = grid;
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
            UpdateLockImage();
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
            UpdateLockImage();
        }

        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(Settings.BpWindowSettings))
            {
                SubscribeBpWindowSettings(_subscribedSettings?.BpWindowSettings);
                UpdateLockImage();
            }
        }

        private void OnBpWindowSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(BpWindowSettings.CurrentBanLockImage)
                || e.PropertyName == nameof(BpWindowSettings.CurrentBanLockImageUri)
                || e.PropertyName == nameof(BpWindowSettings.GlobalBanLockImage)
                || e.PropertyName == nameof(BpWindowSettings.GlobalBanLockImageUri))
            {
                UpdateLockImage();
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

        private void UpdateLockImage()
        {
            if (_lockImage == null)
            {
                return;
            }

            _lockImage.Source = !string.IsNullOrWhiteSpace(_config.LockImageSource)
                ? _resourceResolver.ResolveImage(_config.LockImageSource, FrontedImagePurpose.UiElement)
                : _config.SlotKind == BanSlotKind.Current
                    ? _settingsHostService.Settings.BpWindowSettings.CurrentBanLockImage
                    : _settingsHostService.Settings.BpWindowSettings.GlobalBanLockImage;
        }

        private static bool HasValidIndex(
            ISharedDataService sharedDataService,
            BanSlotDisplayControlConfig config,
            ILogger? logger)
        {
            var count = config.SlotKind switch
            {
                BanSlotKind.Current when config.Camp == Camp.Hun => sharedDataService.CurrentGame.CurrentHunBannedList.Count,
                BanSlotKind.Current => sharedDataService.CurrentGame.CurrentSurBannedList.Count,
                BanSlotKind.Global when config.Camp == Camp.Hun => sharedDataService.CurrentGame.HunTeam.GlobalBannedHunList.Count,
                _ => sharedDataService.CurrentGame.SurTeam.GlobalBannedSurList.Count
            };

            if (config.Index >= 0 && config.Index < count)
            {
                return true;
            }

            logger?.LogWarning(
                "Invalid BanSlotDisplay index. SlotKind: {SlotKind}, Camp: {Camp}, Index: {Index}, Count: {Count}",
                config.SlotKind,
                config.Camp,
                config.Index,
                count);
            return false;
        }

        private static string GetBanImageBindingPath(BanSlotDisplayControlConfig config)
        {
            return config.SlotKind switch
            {
                BanSlotKind.Current when config.Camp == Camp.Hun =>
                    $"CurrentGame.{nameof(Game.CurrentHunBannedList)}[{config.Index}].HeaderImageSingleColor",
                BanSlotKind.Current =>
                    $"CurrentGame.{nameof(Game.CurrentSurBannedList)}[{config.Index}].HeaderImageSingleColor",
                BanSlotKind.Global when config.Camp == Camp.Hun =>
                    $"CurrentGame.HunTeam.{nameof(Team.GlobalBannedHunList)}[{config.Index}].HeaderImageSingleColor",
                _ =>
                    $"CurrentGame.SurTeam.{nameof(Team.GlobalBannedSurList)}[{config.Index}].HeaderImageSingleColor"
            };
        }

        private static string GetCanBanBindingPath(BanSlotDisplayControlConfig config)
        {
            return config.SlotKind switch
            {
                BanSlotKind.Current when config.Camp == Camp.Hun =>
                    $"{nameof(ISharedDataService.CanCurrentHunBannedList)}[{config.Index}]",
                BanSlotKind.Current =>
                    $"{nameof(ISharedDataService.CanCurrentSurBannedList)}[{config.Index}]",
                BanSlotKind.Global when config.Camp == Camp.Hun =>
                    $"{nameof(ISharedDataService.CanGlobalHunBannedList)}[{config.Index}]",
                _ =>
                    $"{nameof(ISharedDataService.CanGlobalSurBannedList)}[{config.Index}]"
            };
        }

        private void ApplyImageLayout(
            Image image,
            BanSlotDisplayControlConfig config,
            ILogger? logger)
        {
            CutSceneFrontedControlHelper.TryApplyEnum<Stretch>(
                config.Stretch,
                value => image.Stretch = value,
                logger,
                nameof(config.Stretch));

            switch (config.SizingMode)
            {
                case ImageSizingMode.FillContainer:
                    BindingOperations.SetBinding(image, FrameworkElement.WidthProperty, new Binding(nameof(ActualWidth))
                    {
                        Source = this
                    });
                    BindingOperations.SetBinding(image, FrameworkElement.HeightProperty, new Binding(nameof(ActualHeight))
                    {
                        Source = this
                    });
                    ApplyAlignment(image, config, logger, HorizontalAlignment.Stretch, VerticalAlignment.Stretch);
                    break;
                case ImageSizingMode.OverflowCrop:
                    ClipToBounds = true;
                    ApplyAlignment(image, config, logger, HorizontalAlignment.Center, VerticalAlignment.Center);
                    break;
                case ImageSizingMode.Auto:
                default:
                    ApplyAlignment(image, config, logger, null, null);
                    break;
            }
        }

        private static void ApplyAlignment(
            Image image,
            BanSlotDisplayControlConfig config,
            ILogger? logger,
            HorizontalAlignment? defaultHorizontalAlignment,
            VerticalAlignment? defaultVerticalAlignment)
        {
            if (string.IsNullOrWhiteSpace(config.HorizontalAlignment) && defaultHorizontalAlignment.HasValue)
            {
                image.HorizontalAlignment = defaultHorizontalAlignment.Value;
            }
            else
            {
                CutSceneFrontedControlHelper.TryApplyEnum<HorizontalAlignment>(
                    config.HorizontalAlignment,
                    value => image.HorizontalAlignment = value,
                    logger,
                    nameof(config.HorizontalAlignment));
            }

            if (string.IsNullOrWhiteSpace(config.VerticalAlignment) && defaultVerticalAlignment.HasValue)
            {
                image.VerticalAlignment = defaultVerticalAlignment.Value;
            }
            else
            {
                CutSceneFrontedControlHelper.TryApplyEnum<VerticalAlignment>(
                    config.VerticalAlignment,
                    value => image.VerticalAlignment = value,
                    logger,
                    nameof(config.VerticalAlignment));
            }
        }
    }

    private sealed class BooleanToReverseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool canBan && !canBan
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
