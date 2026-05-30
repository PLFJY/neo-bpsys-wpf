using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 CutScene 地图名称文本业务控件工厂。
/// </summary>
public class MapNameTextFrontedControl(ILogger<MapNameTextFrontedControl>? logger = null) : IFrontedControl
{
    private readonly ILogger<MapNameTextFrontedControl>? _logger = logger;

    /// <inheritdoc />
    public string ControlType => "MapNameText";

    /// <inheritdoc />
    public Type ConfigType => typeof(MapNameTextControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not MapNameTextControlConfig textConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a MapNameText config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        return new MapNameTextElement(
            name,
            textConfig,
            context.SharedDataService,
            settingsHostService,
            _logger ?? context.Logger);
    }

    private sealed class MapNameTextElement : Border
    {
        private readonly MapNameTextControlConfig _config;
        private readonly ISharedDataService _sharedDataService;
        private readonly ISettingsHostService _settingsHostService;
        private readonly TextBlock _textBlock = new();
        private Game? _subscribedGame;
        private bool _isSubscribed;

        public MapNameTextElement(
            string name,
            MapNameTextControlConfig config,
            ISharedDataService sharedDataService,
            ISettingsHostService settingsHostService,
            ILogger? logger)
        {
            _config = config;
            _sharedDataService = sharedDataService;
            _settingsHostService = settingsHostService;

            var outer = CutSceneFrontedControlHelper.CreateOuterBorder(name, config);
            Name = outer.Name;
            Width = outer.Width;
            Height = outer.Height;
            Canvas.SetLeft(this, Canvas.GetLeft(outer));
            Canvas.SetTop(this, Canvas.GetTop(outer));
            Panel.SetZIndex(this, Panel.GetZIndex(outer));

            CutSceneFrontedControlHelper.ApplyTextStyle(
                _textBlock,
                config.HorizontalAlignment,
                config.VerticalAlignment,
                config.TextAlignment,
                config.FontFamily,
                config.FontWeight,
                config.Color,
                config.FontSize,
                logger);

            Child = _textBlock;
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
            _sharedDataService.CurrentGameChanged += OnCurrentGameChanged;
            _sharedDataService.PickedMapChanged += OnPickedMapChanged;
            _settingsHostService.LanguageSettingChanged += OnLanguageSettingChanged;
            SubscribeGame(_sharedDataService.CurrentGame);
            UpdateText();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isSubscribed)
            {
                return;
            }

            _isSubscribed = false;
            _sharedDataService.CurrentGameChanged -= OnCurrentGameChanged;
            _sharedDataService.PickedMapChanged -= OnPickedMapChanged;
            _settingsHostService.LanguageSettingChanged -= OnLanguageSettingChanged;
            SubscribeGame(null);
        }

        private void OnCurrentGameChanged(object? sender, EventArgs args)
        {
            SubscribeGame(_sharedDataService.CurrentGame);
            UpdateText();
        }

        private void OnPickedMapChanged(object? sender, EventArgs args) => UpdateText();

        private void OnLanguageSettingChanged(object? sender, EventArgs args) => UpdateText();

        private void OnCurrentGamePropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(Game.PickedMap))
            {
                UpdateText();
            }
        }

        private void SubscribeGame(Game? game)
        {
            if (_subscribedGame == game)
            {
                return;
            }

            if (_subscribedGame != null)
            {
                _subscribedGame.PropertyChanged -= OnCurrentGamePropertyChanged;
            }

            _subscribedGame = game;

            if (_subscribedGame != null)
            {
                _subscribedGame.PropertyChanged += OnCurrentGamePropertyChanged;
            }
        }

        private void UpdateText()
        {
            _textBlock.Text = MapNameDisplayHelper.Format(
                _sharedDataService.CurrentGame.PickedMap,
                _config.EmptyText);
        }
    }
}
