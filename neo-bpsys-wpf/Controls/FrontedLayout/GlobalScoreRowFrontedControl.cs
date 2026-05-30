using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 全局比分行控件工厂。
/// </summary>
public class GlobalScoreRowFrontedControl : IFrontedControl
{
    /// <inheritdoc />
    public string ControlType => "GlobalScoreRow";

    /// <inheritdoc />
    public Type ConfigType => typeof(GlobalScoreRowControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not GlobalScoreRowControlConfig rowConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a GlobalScoreRow config.");
        }

        return new GlobalScoreRowElement(name, rowConfig, context.SharedDataService);
    }

    private sealed class GlobalScoreRowElement : Canvas
    {
        private const double DefaultCellWidth = 75;

        private readonly GlobalScoreRowControlConfig _config;
        private readonly ISharedDataService _sharedDataService;
        private MatchScoreState? _subscribedMatchScore;
        private bool _isSubscribed;

        public GlobalScoreRowElement(
            string name,
            GlobalScoreRowControlConfig config,
            ISharedDataService sharedDataService)
        {
            Name = name;
            _config = config;
            _sharedDataService = sharedDataService;

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
            SubscribeMatchScore(_sharedDataService.CurrentGame.MatchScore);
            RenderCells();
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
            SubscribeMatchScore(null);
        }

        private void OnCurrentGameChanged(object? sender, EventArgs args)
        {
            SubscribeMatchScore(_sharedDataService.CurrentGame.MatchScore);
            RenderCells();
        }

        private void OnIsBo3ModeChanged(object? sender, EventArgs args) => RenderCells();

        private void OnMatchScorePropertyChanged(object? sender, PropertyChangedEventArgs args) => RenderCells();

        private void SubscribeMatchScore(MatchScoreState? matchScore)
        {
            if (_subscribedMatchScore == matchScore)
            {
                return;
            }

            if (_subscribedMatchScore != null)
            {
                _subscribedMatchScore.PropertyChanged -= OnMatchScorePropertyChanged;
            }

            _subscribedMatchScore = matchScore;

            if (_subscribedMatchScore != null)
            {
                _subscribedMatchScore.PropertyChanged += OnMatchScorePropertyChanged;
            }
        }

        private void RenderCells()
        {
            Children.Clear();

            var displays = GlobalScoreRowDisplay.Create(
                _sharedDataService.CurrentGame.MatchScore,
                _config.TeamType,
                _sharedDataService.IsBo3Mode,
                _config.MajorGameGap,
                _config.HalfGameGap,
                _config.ShowCampIcon);

            foreach (var display in displays)
            {
                var presenter = CreatePresenter(display);
                Children.Add(presenter);
            }

            if (!_config.Width.HasValue && displays.Count > 0)
            {
                Width = displays.Max(display => display.Left) + DefaultCellWidth;
            }
        }

        private GlobalScorePresenter CreatePresenter(GlobalScoreRowCellDisplay display)
        {
            var presenter = new GlobalScorePresenter
            {
                Name = $"{Name}_{display.GameKey.GameNumber}_{display.GameKey.GameKind}_{display.HalfKind}",
                Width = DefaultCellWidth,
                Text = display.Text,
                IsCampVisible = display.IsCampVisible,
                IsHunIcon = display.IsHunIcon
            };

            Canvas.SetLeft(presenter, display.Left);
            Canvas.SetTop(presenter, 0);
            ApplyTextStyle(presenter);
            return presenter;
        }

        private void ApplyTextStyle(GlobalScorePresenter presenter)
        {
            if (_config.FontSize > 0)
            {
                presenter.FontSize = _config.FontSize;
            }

            if (!string.IsNullOrWhiteSpace(_config.FontWeight))
            {
                try
                {
                    var converter = TypeDescriptor.GetConverter(typeof(FontWeight));
                    if (converter.ConvertFromString(_config.FontWeight) is FontWeight fontWeight)
                    {
                        presenter.FontWeight = fontWeight;
                    }
                }
                catch
                {
                    // Keep default WPF font weight when config is invalid.
                }
            }

            if (!string.IsNullOrWhiteSpace(_config.Color))
            {
                try
                {
                    var converter = TypeDescriptor.GetConverter(typeof(Brush));
                    if (converter.ConvertFromString(_config.Color) is Brush brush)
                    {
                        presenter.Foreground = brush;
                    }
                }
                catch
                {
                    // Keep default foreground when config is invalid.
                }
            }

            if (string.IsNullOrWhiteSpace(_config.FontFamily))
            {
                return;
            }

            presenter.FontFamily = _config.FontFamily.Contains("pack://application:,,,")
                ? new FontFamily(
                    new Uri(_config.FontFamily[.._config.FontFamily.IndexOf('#')]),
                    "./" + _config.FontFamily[_config.FontFamily.IndexOf('#')..])
                : new FontFamily(_config.FontFamily);
        }
    }
}
