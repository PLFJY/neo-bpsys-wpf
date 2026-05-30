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
/// 内置 v3 CutScene 对局进度文本业务控件工厂。
/// </summary>
public class GameProgressTextFrontedControl(ILogger<GameProgressTextFrontedControl>? logger = null) : IFrontedControl
{
    private readonly ILogger<GameProgressTextFrontedControl>? _logger = logger;

    /// <inheritdoc />
    public string ControlType => "GameProgressText";

    /// <inheritdoc />
    public Type ConfigType => typeof(GameProgressTextControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not GameProgressTextControlConfig textConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a GameProgressText config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        return new GameProgressTextElement(
            name,
            textConfig,
            context.SharedDataService,
            settingsHostService,
            _logger ?? context.Logger);
    }

    private sealed class GameProgressTextElement : Border
    {
        private readonly GameProgressTextControlConfig _config;
        private readonly ISharedDataService _sharedDataService;
        private readonly ISettingsHostService _settingsHostService;
        private readonly TextBlock _textBlock = new();
        private Game? _subscribedGame;
        private bool _isSubscribed;

        public GameProgressTextElement(
            string name,
            GameProgressTextControlConfig config,
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
            _sharedDataService.IsBo3ModeChanged += OnIsBo3ModeChanged;
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
            _sharedDataService.IsBo3ModeChanged -= OnIsBo3ModeChanged;
            _settingsHostService.LanguageSettingChanged -= OnLanguageSettingChanged;
            SubscribeGame(null);
        }

        private void OnCurrentGameChanged(object? sender, EventArgs args)
        {
            SubscribeGame(_sharedDataService.CurrentGame);
            UpdateText();
        }

        private void OnIsBo3ModeChanged(object? sender, EventArgs args) => UpdateText();

        private void OnLanguageSettingChanged(object? sender, EventArgs args) => UpdateText();

        private void OnCurrentGamePropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(Game.GameProgress))
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
            _textBlock.Text = GameProgressDisplayHelper.Format(
                _sharedDataService.CurrentGame.GameProgress,
                _sharedDataService.IsBo3Mode,
                _config.UseLineBreak);
        }
    }
}
