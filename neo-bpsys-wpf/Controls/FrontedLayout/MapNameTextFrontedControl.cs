using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.PluginSdk;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 CutScene 地图名称文本业务控件。
/// </summary>
[FrontedV3Control("MapNameText", IsBuiltIn = true)]
public class MapNameTextFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not MapNameTextControlConfig textConfig)
        {
            throw new FrontedLayoutConfigException("Control config is not a MapNameText config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        var element = new MapNameTextElement(
            context.ControlName ?? string.Empty,
            textConfig,
            context.SharedDataService,
            settingsHostService,
            context.Logger);
        Content = element;
    }

    private sealed class MapNameTextElement : Border
    {
        public static readonly DependencyProperty MapValueProperty =
            DependencyProperty.Register(
                nameof(MapValue),
                typeof(object),
                typeof(MapNameTextElement),
                new PropertyMetadata(null, OnMapValueChanged));

        private readonly MapNameTextControlConfig _config;
        private readonly ISettingsHostService _settingsHostService;
        private readonly TextBlock _textBlock = new();
        private CultureInfo _currentAppCulture;
        private bool _isSubscribed;

        public object? MapValue
        {
            get => GetValue(MapValueProperty);
            set => SetValue(MapValueProperty, value);
        }

        public MapNameTextElement(
            string name,
            MapNameTextControlConfig config,
            ISharedDataService sharedDataService,
            ISettingsHostService settingsHostService,
            ILogger? logger)
        {
            _config = config;
            _settingsHostService = settingsHostService;
            _currentAppCulture = settingsHostService.Settings.CultureInfo;

            Name = name;

            CutSceneFrontedControlHelper.ApplyTextStyle(
                _textBlock,
                config.HorizontalAlignment,
                config.VerticalAlignment,
                config.TextAlignment,
                config.FontFamily,
                config.FontWeight,
                config.Color,
                config.ColorBindingPath,
                config.FontSize,
                sharedDataService,
                logger);

            Child = _textBlock;
            BindingOperations.SetBinding(this, MapValueProperty, new Binding(GetBindingPath(config))
            {
                Source = sharedDataService
            });
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
            _settingsHostService.LanguageSettingChanged += OnLanguageSettingChanged;
            UpdateText();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isSubscribed)
            {
                return;
            }

            _isSubscribed = false;
            _settingsHostService.LanguageSettingChanged -= OnLanguageSettingChanged;
        }

        private void OnLanguageSettingChanged(object? sender, LanguageChangedEventArgs args)
        {
            _currentAppCulture = args.CultureInfo;
            UpdateText();
        }

        private static string GetBindingPath(MapNameTextControlConfig config)
        {
            return string.IsNullOrWhiteSpace(config.BindingPath)
                ? "CurrentGame.PickedMap"
                : config.BindingPath;
        }

        private static void OnMapValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is MapNameTextElement element)
            {
                element.UpdateText();
            }
        }

        private void UpdateText()
        {
            _textBlock.Text = MapNameDisplayHelper.Format(
                CoerceMap(MapValue),
                _config.EmptyText,
                _currentAppCulture);
        }

        private static Map? CoerceMap(object? value)
        {
            return value switch
            {
                null => null,
                Map map => map,
                string text when Enum.TryParse<Map>(text, true, out var map) => map,
                _ => null
            };
        }
    }
}
