using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// 使用活动 v3 布局实时编辑自由对局局内比分的模态窗口。
/// </summary>
public partial class FreeInGameScoreEditorWindow : FluentWindow
{
    private readonly IFrontedLayoutService _layoutService;
    private readonly IFrontedRenderer _renderer;
    private readonly ISharedDataService _sharedDataService;

    /// <summary>初始化自由对局局内比分编辑窗口。</summary>
    /// <param name="layoutService">前台布局服务。</param>
    /// <param name="renderer">前台渲染器。</param>
    /// <param name="sharedDataService">共享数据服务。</param>
    public FreeInGameScoreEditorWindow(
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
        if (_sharedDataService.CurrentGame.GameProgress != GameProgress.Free)
        {
            Close();
            return;
        }

        await RenderWindowAsync("ScoreSurWindow", SurPreviewCanvas, SurHitCanvas, Camp.Sur);
        await RenderWindowAsync("ScoreHunWindow", HunPreviewCanvas, HunHitCanvas, Camp.Hun);
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        _sharedDataService.CurrentGameChanged -= OnScoreContextChanged;
        _sharedDataService.GameProgressChanged -= OnScoreContextChanged;
    }

    private void OnScoreContextChanged(object? sender, EventArgs args) => Dispatcher.BeginInvoke(Close);

    private async Task RenderWindowAsync(string windowId, Canvas preview, Canvas hits, Camp camp)
    {
        var config = await _layoutService.LoadWindowConfigAsync(windowId);
        preview.Width = hits.Width = config.CanvasSettings.CanvasWidth;
        preview.Height = hits.Height = config.CanvasSettings.CanvasHeight;
        _renderer.RenderToCanvas(preview, config, new FrontedRenderContext
        {
            WindowId = windowId,
            WindowTypeName = windowId,
            CanvasName = "BaseCanvas",
            IsDesignerPreview = false
        });

        foreach (var control in config.ControlLayout.Controls.Values.OfType<TextFrontedControlConfig>())
        {
            var path = control.TextBinding?.Sources.FirstOrDefault()?.Path;
            var field = ResolveField(path, camp);
            if (field is null)
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
            button.Click += (_, _) => SelectField(camp, field.Value);
            Canvas.SetLeft(button, control.Left);
            Canvas.SetTop(button, control.Top);
            hits.Children.Add(button);
        }
    }

    private static FreeScoreEditorField? ResolveField(string? path, Camp camp)
    {
        var prefix = camp == Camp.Sur ? "CurrentSurTeam" : "CurrentHunTeam";
        if (path?.EndsWith(prefix + "MinorScoreText", StringComparison.Ordinal) == true)
            return FreeScoreEditorField.CurrentMinor;
        if (path?.EndsWith(prefix + "TotalMinorScore", StringComparison.Ordinal) == true)
            return FreeScoreEditorField.TotalMinor;
        if (path?.EndsWith(prefix + "MajorText", StringComparison.Ordinal) == true)
            return FreeScoreEditorField.Major;
        return null;
    }

    private void SelectField(Camp camp, FreeScoreEditorField field)
    {
        var teamType = camp == Camp.Sur
            ? _sharedDataService.CurrentGame.SurTeam.TeamType
            : _sharedDataService.CurrentGame.HunTeam.TeamType;
        var state = teamType == TeamType.HomeTeam
            ? _sharedDataService.CurrentGame.MatchScore.FreeScore.Home
            : _sharedDataService.CurrentGame.MatchScore.FreeScore.Away;

        EditorPanel.Visibility = Visibility.Visible;
        EditorPanel.IsEnabled = true;
        SelectionText.Text = camp == Camp.Sur ? Loc("EditingSurvivorScore") : Loc("EditingHunterScore");
        SecondaryLabel.Visibility = SecondaryValueBox.Visibility =
            field == FreeScoreEditorField.Major ? Visibility.Visible : Visibility.Collapsed;

        var primaryPath = field switch
        {
            FreeScoreEditorField.CurrentMinor => nameof(FreeTeamScoreState.CurrentMinorScore),
            FreeScoreEditorField.TotalMinor => nameof(FreeTeamScoreState.TotalMinorScore),
            _ => nameof(FreeTeamScoreState.MajorWin)
        };
        PrimaryLabel.Text = Loc(field switch
        {
            FreeScoreEditorField.CurrentMinor => "CurrentMinorScore",
            FreeScoreEditorField.TotalMinor => "TotalMinorScore",
            _ => "MajorWin"
        });
        SecondaryLabel.Text = Loc("MajorTie");
        BindNumberBox(PrimaryValueBox, state, primaryPath);
        BindNumberBox(SecondaryValueBox, state, nameof(FreeTeamScoreState.MajorTie));
    }

    private static void BindNumberBox(NumberBox numberBox, object source, string path)
    {
        BindingOperations.SetBinding(numberBox, NumberBox.ValueProperty, new Binding(path)
        {
            Source = source,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
    }

    private static string Loc(string key) =>
        I18nHelper.GetLocalizedString(AppI18nDictionaries.Score, key);

    private enum FreeScoreEditorField
    {
        CurrentMinor,
        TotalMinor,
        Major
    }
}
