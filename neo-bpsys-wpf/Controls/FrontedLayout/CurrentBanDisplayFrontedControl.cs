using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 当前局 Ban 位显示控件工厂。
/// </summary>
public class CurrentBanDisplayFrontedControl(ILogger<CurrentBanDisplayFrontedControl>? logger = null) : IFrontedControl
{
    private readonly ILogger<CurrentBanDisplayFrontedControl>? _logger = logger;

    /// <inheritdoc />
    public string ControlType => "CurrentBanDisplay";

    /// <inheritdoc />
    public Type ConfigType => typeof(CurrentBanDisplayControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not CurrentBanDisplayControlConfig banConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a CurrentBanDisplay config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        return new CurrentBanDisplayElement(
            name,
            banConfig,
            context.SharedDataService,
            settingsHostService,
            _logger ?? context.Logger);
    }

    private sealed class CurrentBanDisplayElement : Border
    {
        private static readonly BooleanToReverseVisibilityConverter ReverseVisibilityConverter = new();

        private readonly ISettingsHostService _settingsHostService;
        private readonly Image? _lockImage;
        private WidgetsWindowSettings? _subscribedSettings;
        private bool _isSubscribed;

        public CurrentBanDisplayElement(
            string name,
            CurrentBanDisplayControlConfig config,
            ISharedDataService sharedDataService,
            ISettingsHostService settingsHostService,
            ILogger? logger)
        {
            _settingsHostService = settingsHostService;

            var outer = CutSceneFrontedControlHelper.CreateOuterBorder(name, config);
            Name = outer.Name;
            Width = outer.Width;
            Height = outer.Height;
            Canvas.SetLeft(this, Canvas.GetLeft(outer));
            Canvas.SetTop(this, Canvas.GetTop(outer));
            Panel.SetZIndex(this, Panel.GetZIndex(outer));

            var grid = new Grid();
            var banImage = new Image();
            ApplyImageStyle(banImage, config, logger);

            if (HasValidIndex(sharedDataService, config, logger))
            {
                BindingOperations.SetBinding(banImage, Image.SourceProperty, new Binding(GetBanImageBindingPath(config))
                {
                    Source = sharedDataService
                });
            }

            grid.Children.Add(banImage);

            if (config.ShowLockOverlay)
            {
                _lockImage = new Image();
                ApplyImageStyle(_lockImage, config, logger);
                BindingOperations.SetBinding(_lockImage, VisibilityProperty, new Binding(GetCanBanBindingPath(config))
                {
                    Source = sharedDataService,
                    Converter = ReverseVisibilityConverter
                });
                Panel.SetZIndex(_lockImage, 1);
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
            SubscribeSettings(_settingsHostService.Settings.WidgetsWindowSettings);
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
            SubscribeSettings(settings.WidgetsWindowSettings);
            UpdateLockImage();
        }

        private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(WidgetsWindowSettings.CurrentBanLockImage)
                || e.PropertyName == nameof(WidgetsWindowSettings.CurrentBanLockImageUri))
            {
                UpdateLockImage();
            }
        }

        private void SubscribeSettings(WidgetsWindowSettings? settings)
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
        }

        private void UpdateLockImage()
        {
            if (_lockImage != null)
            {
                _lockImage.Source = _settingsHostService.Settings.WidgetsWindowSettings.CurrentBanLockImage;
            }
        }

        private static bool HasValidIndex(
            ISharedDataService sharedDataService,
            CurrentBanDisplayControlConfig config,
            ILogger? logger)
        {
            var count = config.Camp == Camp.Hun
                ? sharedDataService.CurrentGame.CurrentHunBannedList.Count
                : sharedDataService.CurrentGame.CurrentSurBannedList.Count;

            if (config.Index >= 0 && config.Index < count)
            {
                return true;
            }

            logger?.LogWarning(
                "Invalid CurrentBanDisplay index. Camp: {Camp}, Index: {Index}, Count: {Count}",
                config.Camp,
                config.Index,
                count);
            return false;
        }

        private static string GetBanImageBindingPath(CurrentBanDisplayControlConfig config)
        {
            var listName = config.Camp == Camp.Hun
                ? nameof(Game.CurrentHunBannedList)
                : nameof(Game.CurrentSurBannedList);

            return $"CurrentGame.{listName}[{config.Index}].HeaderImageSingleColor";
        }

        private static string GetCanBanBindingPath(CurrentBanDisplayControlConfig config)
        {
            var listName = config.Camp == Camp.Hun
                ? nameof(ISharedDataService.CanCurrentHunBannedList)
                : nameof(ISharedDataService.CanCurrentSurBannedList);

            return $"{listName}[{config.Index}]";
        }

        private static void ApplyImageStyle(
            Image image,
            CurrentBanDisplayControlConfig config,
            ILogger? logger)
        {
            CutSceneFrontedControlHelper.TryApplyEnum<Stretch>(
                config.Stretch,
                value => image.Stretch = value,
                logger,
                nameof(config.Stretch));
            CutSceneFrontedControlHelper.TryApplyEnum<HorizontalAlignment>(
                config.HorizontalAlignment,
                value => image.HorizontalAlignment = value,
                logger,
                nameof(config.HorizontalAlignment));
            CutSceneFrontedControlHelper.TryApplyEnum<VerticalAlignment>(
                config.VerticalAlignment,
                value => image.VerticalAlignment = value,
                logger,
                nameof(config.VerticalAlignment));
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
