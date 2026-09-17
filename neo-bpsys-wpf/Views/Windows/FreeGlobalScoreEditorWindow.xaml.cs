using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// 使用活动 v3 ScoreGlobal 布局实时编辑自由全局比分的模态窗口。
/// </summary>
public partial class FreeGlobalScoreEditorWindow : FluentWindow
{
    private readonly IFrontedLayoutService _layoutService;
    private readonly IFrontedRenderer _renderer;
    private readonly ISharedDataService _sharedDataService;
    private ScoreGameKey? _selectedGameKey;
    private ScoreHalfKind? _selectedHalfKind;
    private FreeGlobalScoreHalf? _selectedHalf;
    private MatchScoreState? _subscribedMatchScore;
    private TeamType _presetSurvivorTeam = TeamType.HomeTeam;
    private bool _rewireScheduled;

    /// <summary>初始化自由 ScoreGlobal 编辑窗口。</summary>
    /// <param name="layoutService">前台布局服务。</param>
    /// <param name="renderer">前台渲染器。</param>
    /// <param name="sharedDataService">共享数据服务。</param>
    public FreeGlobalScoreEditorWindow(
        IFrontedLayoutService layoutService,
        IFrontedRenderer renderer,
        ISharedDataService sharedDataService)
    {
        _layoutService = layoutService;
        _renderer = renderer;
        _sharedDataService = sharedDataService;
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        _sharedDataService.CurrentGameChanged += OnScoreContextChanged;
        _sharedDataService.GameProgressChanged += OnScoreContextChanged;
        _subscribedMatchScore = _sharedDataService.CurrentGame.MatchScore;
        _subscribedMatchScore.PropertyChanged += OnMatchScorePropertyChanged;
        if (_sharedDataService.CurrentGame.GameProgress != GameProgress.Free)
        {
            Close();
            return;
        }

        var config = await _layoutService.LoadWindowConfigAsync("ScoreGlobalWindow");
        PreviewCanvas.Width = TotalHitCanvas.Width = config.CanvasSettings.CanvasWidth;
        PreviewCanvas.Height = TotalHitCanvas.Height = config.CanvasSettings.CanvasHeight;
        _renderer.RenderToCanvas(PreviewCanvas, config, new FrontedRenderContext
        {
            WindowId = "ScoreGlobalWindow",
            WindowTypeName = "ScoreGlobalWindow",
            CanvasName = "BaseCanvas",
            IsDesignerPreview = false
        });
        AddTotalHitTargets(config);
        SchedulePresenterWiring();
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        _sharedDataService.CurrentGameChanged -= OnScoreContextChanged;
        _sharedDataService.GameProgressChanged -= OnScoreContextChanged;
        if (_subscribedMatchScore is not null)
            _subscribedMatchScore.PropertyChanged -= OnMatchScorePropertyChanged;
        if (_selectedHalf is not null)
            _selectedHalf.PropertyChanged -= OnSelectedHalfPropertyChanged;
    }

    private void OnScoreContextChanged(object? sender, EventArgs args) => Dispatcher.BeginInvoke(Close);

    private void OnMatchScorePropertyChanged(object? sender, PropertyChangedEventArgs args) => SchedulePresenterWiring();

    private void SchedulePresenterWiring()
    {
        if (_rewireScheduled)
            return;
        _rewireScheduled = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            () =>
            {
                _rewireScheduled = false;
                WirePresenters();
            });
    }

    private void WirePresenters()
    {
        var presenters = EnumerateVisuals(PreviewCanvas).OfType<GlobalScorePresenter>().ToList();
        foreach (var presenter in presenters)
        {
            presenter.PreviewMouseLeftButtonDown -= Presenter_MouseLeftButtonDown;
            presenter.PreviewMouseLeftButtonDown += Presenter_MouseLeftButtonDown;
            if (_selectedGameKey.HasValue &&
                _selectedHalfKind.HasValue &&
                presenter.ScoreGameKey == _selectedGameKey.Value &&
                presenter.ScoreHalfKind == _selectedHalfKind.Value)
            {
                HighlightPresenter(presenter);
            }
        }

    }

    private void AddTotalHitTargets(FrontedWindowConfig config)
    {
        var canvasConfig = FrontedWindowConfigCanvasAdapter.ToCanvasConfig(config);
        var runtimeState = FrontedCanvasRuntimeStateResolver.Resolve(canvasConfig, _sharedDataService);
        foreach (var control in runtimeState.Controls.Values.OfType<TextFrontedControlConfig>())
        {
            var path = control.TextBinding?.Sources.FirstOrDefault()?.Path;
            var teamType = path switch
            {
                "CurrentGame.MatchScore.HomeTotalMinorScore" => TeamType.HomeTeam,
                "CurrentGame.MatchScore.AwayTotalMinorScore" => TeamType.AwayTeam,
                _ => (TeamType?)null
            };
            if (!teamType.HasValue)
                continue;

            var button = new System.Windows.Controls.Button
            {
                Width = control.Width ?? 90,
                Height = control.Height ?? 44,
                Background = Brushes.Transparent,
                BorderBrush = TryFindResource("SystemFillColorCautionBrush") as Brush ?? Brushes.Gold,
                BorderThickness = new Thickness(2),
                ToolTip = Loc("ClickToEditScore")
            };
            button.Click += (_, _) => SelectTotalScore(teamType.Value);
            Canvas.SetLeft(button, control.Left);
            Canvas.SetTop(button, control.Top);
            TotalHitCanvas.Children.Add(button);
        }
    }

    private void Presenter_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs args)
    {
        if (sender is not GlobalScorePresenter presenter)
            return;

        ClearTotalSelection();
        ClearPresenterHighlights();

        _selectedGameKey = presenter.ScoreGameKey;
        _selectedHalfKind = presenter.ScoreHalfKind;
        HighlightSelectedHalf(presenter.ScoreGameKey, presenter.ScoreHalfKind);
        var previousHalf = _selectedHalf;
        _selectedHalf = _sharedDataService.CurrentGame.MatchScore.FreeScore.GetGlobalHalf(
            presenter.ScoreGameKey,
            presenter.ScoreHalfKind);
        if (previousHalf is not null)
            previousHalf.PropertyChanged -= OnSelectedHalfPropertyChanged;
        if (_selectedHalf is null)
            return;

        EditorPanel.DataContext = _selectedHalf;
        CompletedCheckBox.DataContext = _selectedHalf;
        CompletedCheckBox.Visibility = Visibility.Visible;
        EditorPanel.Visibility = Visibility.Visible;
        EditorPanel.IsEnabled = _selectedHalf.IsCompleted;
        _selectedHalf.PropertyChanged += OnSelectedHalfPropertyChanged;
        _presetSurvivorTeam = _selectedHalf.HomeCamp == Camp.Hun && _selectedHalf.AwayCamp == Camp.Sur
            ? TeamType.AwayTeam
            : TeamType.HomeTeam;
        HomeSurvivorPresetRadio.IsChecked = _presetSurvivorTeam == TeamType.HomeTeam;
        AwaySurvivorPresetRadio.IsChecked = _presetSurvivorTeam == TeamType.AwayTeam;
        Escape4PresetRadio.IsChecked = false;
        Escape3PresetRadio.IsChecked = false;
        TiePresetRadio.IsChecked = false;
        Out3PresetRadio.IsChecked = false;
        Out4PresetRadio.IsChecked = false;
        SelectionText.Text = FormatSelection(presenter.ScoreGameKey, presenter.ScoreHalfKind);
        args.Handled = true;
    }

    private void SelectTotalScore(TeamType teamType)
    {
        ClearTotalSelection();
        ClearPresenterHighlights();
        EditorPanel.Visibility = Visibility.Collapsed;
        CompletedCheckBox.Visibility = Visibility.Collapsed;
        TotalEditorPanel.Visibility = Visibility.Visible;

        var score = teamType == TeamType.HomeTeam
            ? _sharedDataService.CurrentGame.MatchScore.FreeScore.Home
            : _sharedDataService.CurrentGame.MatchScore.FreeScore.Away;
        BindingOperations.SetBinding(TotalValueBox, NumberBox.ValueProperty, new Binding(nameof(FreeTeamScoreState.TotalMinorScore))
        {
            Source = score,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        TotalLabel.Text = Loc("TotalMinorScore");
        SelectionText.Text = teamType == TeamType.HomeTeam
            ? _sharedDataService.HomeTeam.Name
            : _sharedDataService.AwayTeam.Name;
    }

    private void ClearTotalSelection()
    {
        _selectedGameKey = null;
        _selectedHalfKind = null;
        if (_selectedHalf is not null)
            _selectedHalf.PropertyChanged -= OnSelectedHalfPropertyChanged;
        _selectedHalf = null;
        BindingOperations.ClearBinding(TotalValueBox, NumberBox.ValueProperty);
        TotalEditorPanel.Visibility = Visibility.Collapsed;
    }

    private void OnSelectedHalfPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(FreeGlobalScoreHalf.IsCompleted) && _selectedHalf is not null)
            EditorPanel.IsEnabled = _selectedHalf.IsCompleted;
    }

    private void CompletedCheckBox_Checked(object sender, RoutedEventArgs args)
    {
        if (_selectedHalf is null || _selectedHalf.IsInitialized)
            return;

        _selectedHalf.HomeMinorScore = 0;
        _selectedHalf.AwayMinorScore = 0;
        _selectedHalf.HomeCamp = _sharedDataService.CurrentGame.SurTeam.TeamType == TeamType.HomeTeam
            ? Camp.Sur
            : Camp.Hun;
        _selectedHalf.AwayCamp = _sharedDataService.CurrentGame.SurTeam.TeamType == TeamType.AwayTeam
            ? Camp.Sur
            : Camp.Hun;
        _presetSurvivorTeam = _selectedHalf.HomeCamp == Camp.Sur ? TeamType.HomeTeam : TeamType.AwayTeam;
        _selectedHalf.IsInitialized = true;
    }

    private void HomeSurvivorPreset_Click(object sender, RoutedEventArgs args) => SetPresetCamps(TeamType.HomeTeam);

    private void AwaySurvivorPreset_Click(object sender, RoutedEventArgs args) => SetPresetCamps(TeamType.AwayTeam);

    private void SetPresetCamps(TeamType survivorTeam)
    {
        if (_selectedHalf is null)
            return;
        _presetSurvivorTeam = survivorTeam;
        _selectedHalf.HomeCamp = survivorTeam == TeamType.HomeTeam ? Camp.Sur : Camp.Hun;
        _selectedHalf.AwayCamp = survivorTeam == TeamType.AwayTeam ? Camp.Sur : Camp.Hun;
        _selectedHalf.IsInitialized = true;
    }

    private void ResultPreset_Click(object sender, RoutedEventArgs args)
    {
        if (_selectedHalf is null || sender is not FrameworkElement { Tag: GameResult result })
            return;
        var (sur, hun) = result switch
        {
            GameResult.Escape4 => (5, 0),
            GameResult.Escape3 => (3, 1),
            GameResult.Tie => (2, 2),
            GameResult.Out3 => (1, 3),
            GameResult.Out4 => (0, 5),
            _ => throw new InvalidEnumArgumentException(nameof(result), (int)result, typeof(GameResult))
        };
        _selectedHalf.HomeMinorScore = _presetSurvivorTeam == TeamType.HomeTeam ? sur : hun;
        _selectedHalf.AwayMinorScore = _presetSurvivorTeam == TeamType.AwayTeam ? sur : hun;
        SetPresetCamps(_presetSurvivorTeam);
    }

    private void HighlightPresenter(GlobalScorePresenter presenter)
    {
        presenter.BorderBrush = TryFindResource("SystemFillColorCautionBrush") as Brush ?? Brushes.Gold;
        presenter.BorderThickness = new Thickness(2);
    }

    private void HighlightSelectedHalf(ScoreGameKey key, ScoreHalfKind halfKind)
    {
        foreach (var presenter in EnumerateVisuals(PreviewCanvas).OfType<GlobalScorePresenter>())
        {
            if (presenter.ScoreGameKey == key && presenter.ScoreHalfKind == halfKind)
                HighlightPresenter(presenter);
        }
    }

    private void ClearPresenterHighlights()
    {
        foreach (var presenter in EnumerateVisuals(PreviewCanvas).OfType<GlobalScorePresenter>())
        {
            presenter.BorderThickness = new Thickness(0);
            presenter.BorderBrush = Brushes.Transparent;
        }
    }

    private static IEnumerable<DependencyObject> EnumerateVisuals(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in EnumerateVisuals(VisualTreeHelper.GetChild(root, index)))
                yield return child;
        }
    }

    private static string FormatSelection(ScoreGameKey key, ScoreHalfKind halfKind)
    {
        var gameFormatKey = key.GameKind == ScoreGameKind.Overtime
            ? "ScorePreviewGameOvertimeFormat"
            : "ScorePreviewGameFormat";
        var game = string.Format(
            LocalizeDictionary.CurrentCulture,
            Loc(gameFormatKey),
            key.GameNumber);
        var half = Loc(halfKind == ScoreHalfKind.FirstHalf
            ? "ScorePreviewFirstHalf"
            : "ScorePreviewSecondHalf");
        return string.Format(
            LocalizeDictionary.CurrentCulture,
            Loc("ScorePreviewProgressFormat"),
            game,
            half);
    }

    private static string Loc(string key) =>
        I18nHelper.GetLocalizedString(AppI18nDictionaries.Score, key, LocalizeDictionary.CurrentCulture);
}
