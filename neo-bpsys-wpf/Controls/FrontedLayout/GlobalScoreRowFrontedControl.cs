using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 全局比分行控件。
/// </summary>
[FrontedV3Control("GlobalScoreRow", IsBuiltIn = true)]
public class GlobalScoreRowFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not GlobalScoreRowControlConfig rowConfig)
        {
            throw new FrontedLayoutConfigException("Control config is not a GlobalScoreRow config.");
        }

        var element = new GlobalScoreRowElement(
            context.ControlName ?? string.Empty,
            rowConfig,
            context.SharedDataService);
        Content = element;
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
            SubscribeMatchScore(null);
        }

        private void OnCurrentGameChanged(object? sender, EventArgs args)
        {
            SubscribeMatchScore(_sharedDataService.CurrentGame.MatchScore);
            RenderCells();
        }

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

            var cells = _config.Cells.Count > 0
                ? _config.Cells
#pragma warning disable CS0618
                : GlobalScoreRowDisplay.CreateDefaultCells(
                    _sharedDataService.CurrentGame.MatchScore,
                    _sharedDataService.IsBo3Mode,
                    _config.MajorGameGap,
                    _config.HalfGameGap);
#pragma warning restore CS0618

            foreach (var cell in cells)
            {
                var display = GlobalScoreRowDisplay.Create(
                    _sharedDataService.CurrentGame.MatchScore,
                    _config.TeamType,
                    cell,
                    cell.ShowCampIcon ?? _config.ShowCampIcon);
                var presenter = CreatePresenter(cell, display);
                Children.Add(presenter);
            }

            if (!_config.Width.HasValue && cells.Count > 0)
            {
                Width = cells.Max(cell => cell.X + cell.Width);
            }

            if (!_config.Height.HasValue && cells.Count > 0)
            {
                Height = cells.Max(cell => cell.Y + cell.Height);
            }
        }

        private GlobalScorePresenter CreatePresenter(GlobalScoreCellConfig cell, GlobalScoreRowCellDisplay display)
        {
            var presenter = new GlobalScorePresenter
            {
                Name = $"{Name}_{SanitizeName(cell.Id, display)}",
                Width = cell.Width > 0 ? cell.Width : DefaultCellWidth,
                Height = cell.Height > 0 ? cell.Height : double.NaN,
                Text = display.Text,
                IsCampVisible = display.IsCampVisible,
                IsHunIcon = display.IsHunIcon,
                CampIconColor = cell.CampIconColor ?? _config.CampIconColor,
                Visibility = MapVisibility(cell.Visibility)
            };

            Canvas.SetLeft(presenter, display.Left);
            Canvas.SetTop(presenter, display.Top);
            ApplyTextStyle(presenter, cell);
            return presenter;
        }

        private void ApplyTextStyle(GlobalScorePresenter presenter, GlobalScoreCellConfig cell)
        {
            var fontSize = cell.FontSize ?? _config.FontSize;
            var fontWeightText = cell.FontWeight ?? _config.FontWeight;
            var colorText = cell.Color ?? _config.Color;
            var fontFamilyText = cell.FontFamily ?? _config.FontFamily;

            if (fontSize > 0)
            {
                presenter.FontSize = fontSize;
            }

            if (!string.IsNullOrWhiteSpace(fontWeightText))
            {
                try
                {
                    var converter = TypeDescriptor.GetConverter(typeof(FontWeight));
                    if (converter.ConvertFromString(fontWeightText) is FontWeight fontWeight)
                    {
                        presenter.FontWeight = fontWeight;
                    }
                }
                catch
                {
                    // Keep default WPF font weight when config is invalid.
                }
            }

            if (!string.IsNullOrWhiteSpace(colorText))
            {
                try
                {
                    var converter = TypeDescriptor.GetConverter(typeof(Brush));
                    if (converter.ConvertFromString(colorText) is Brush brush)
                    {
                        presenter.Foreground = brush;
                    }
                }
                catch
                {
                    // Keep default foreground when config is invalid.
                }
            }

            if (string.IsNullOrWhiteSpace(fontFamilyText))
            {
                return;
            }

            presenter.FontFamily = FrontedFontResourceHelper.CreateFontFamily(fontFamilyText);
        }

        private static Visibility MapVisibility(FrontedControlVisibility visibility) =>
            visibility switch
            {
                FrontedControlVisibility.Hidden => Visibility.Hidden,
                FrontedControlVisibility.Collapsed => Visibility.Collapsed,
                _ => Visibility.Visible
            };

        private static string SanitizeName(GlobalScoreRowCellDisplay display) =>
            $"{display.GameKey.GameNumber}_{display.GameKey.GameKind}_{display.HalfKind}";

        private static string SanitizeName(string? id, GlobalScoreRowCellDisplay display)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return SanitizeName(display);
            }

            var chars = id
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '_')
                .ToArray();
            return chars.Length > 0 ? new string(chars) : SanitizeName(display);
        }
    }
}
