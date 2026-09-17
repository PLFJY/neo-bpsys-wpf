using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Tutorial;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Team = neo_bpsys_wpf.Core.Models.Team;
using WPFLocalizeExtension.Engine;
using neo_bpsys_wpf.Views.Windows;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 比分预览行数据，用于在比分页面表格中展示单场/半场信息。
/// </summary>
public sealed class ScorePreviewRow
{
    /// <summary>
    /// 构造比分预览行数据。
    /// </summary>
    public ScorePreviewRow(
        string gameLabel,
        string halfLabel,
        GameProgress progress,
        string progressText,
        string resultText,
        string homeCampText,
        string awayCampText,
        string homeMinorScoreText,
        string awayMinorScoreText,
        string homeTotalMinorScoreText,
        string awayTotalMinorScoreText,
        bool hasResult,
        bool isCurrentProgress,
        string rowStatusText,
        string homeTeamName,
        string awayTeamName)
    {
        GameLabel = gameLabel;
        HalfLabel = halfLabel;
        Progress = progress;
        ProgressText = progressText;
        ResultText = resultText;
        HomeCampText = homeCampText;
        AwayCampText = awayCampText;
        HomeMinorScoreText = homeMinorScoreText;
        AwayMinorScoreText = awayMinorScoreText;
        HomeTotalMinorScoreText = homeTotalMinorScoreText;
        AwayTotalMinorScoreText = awayTotalMinorScoreText;
        HasResult = hasResult;
        IsCurrentProgress = isCurrentProgress;
        RowStatusText = rowStatusText;
        HomeTeamName = homeTeamName;
        AwayTeamName = awayTeamName;
    }

    /// <summary>比赛标签（如"第 1 局"）。</summary>
    public string GameLabel { get; }

    /// <summary>半场标签（"上半场" / "下半场"）。</summary>
    public string HalfLabel { get; }

    /// <summary>比赛进度。</summary>
    public GameProgress Progress { get; }

    /// <summary>进度文本。</summary>
    public string ProgressText { get; }

    /// <summary>结果文本。</summary>
    public string ResultText { get; }

    /// <summary>主队阵营文本。</summary>
    public string HomeCampText { get; }

    /// <summary>客队阵营文本。</summary>
    public string AwayCampText { get; }

    /// <summary>主队小分文本。</summary>
    public string HomeMinorScoreText { get; }

    /// <summary>客队小分文本。</summary>
    public string AwayMinorScoreText { get; }

    /// <summary>主队总小分文本。</summary>
    public string HomeTotalMinorScoreText { get; }

    /// <summary>客队总小分文本。</summary>
    public string AwayTotalMinorScoreText { get; }

    /// <summary>是否有结果。</summary>
    public bool HasResult { get; }

    /// <summary>是否为当前进度。</summary>
    public bool IsCurrentProgress { get; }

    /// <summary>行状态文本。</summary>
    public string RowStatusText { get; }

    /// <summary>主队名称。</summary>
    public string HomeTeamName { get; }

    /// <summary>客队名称。</summary>
    public string AwayTeamName { get; }
}

/// <summary>
/// 比分页面视图模型，管理比赛比分控制、比分预览行展示和比分操作。
/// </summary>
public partial class ScorePageViewModel : ViewModelBase
{
#pragma warning disable CS8618
    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
    public ScorePageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly IMatchScoreService _matchScoreService;
    private Game? _subscribedGame;
    private MatchScoreState? _subscribedMatchScore;

    /// <summary>
    /// 初始化比分页面视图模型。
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    /// <param name="matchScoreService">比赛比分服务</param>
    public ScorePageViewModel(
        ISharedDataService sharedDataService,
        IMatchScoreService matchScoreService)
    {
        _sharedDataService = sharedDataService;
        _matchScoreService = matchScoreService;
        _sharedDataService.CurrentGameChanged += OnCurrentGameChanged;
        _sharedDataService.IsBo3ModeChanged += OnIsBo3ModeChanged;
        LocalizeDictionary.Instance.PropertyChanged += OnLocalizeDictionaryPropertyChanged;
        SubscribeGame(_sharedDataService.CurrentGame);
        RefreshScorePageState();
    }

    /// <summary>获取当前比赛数据。</summary>
    public Game CurrentGame => _sharedDataService.CurrentGame;

    /// <summary>获取主队数据。</summary>
    public Team HomeTeam => _sharedDataService.HomeTeam;

    /// <summary>获取客队数据。</summary>
    public Team AwayTeam => _sharedDataService.AwayTeam;

    /// <summary>获取当前是否处于自由对局模式。</summary>
    public bool IsFreeMode => _matchScoreService.IsFreeMode;

    /// <summary>
    /// 获取或设置当前半场的比赛结果。
    /// </summary>
    public GameResult? SelectedCurrentHalfResult
    {
        get => _matchScoreService.CurrentHalf?.Result;
        set
        {
            if (IsFreeMode || _matchScoreService.CurrentHalf?.Result == value)
                return;

            _matchScoreService.SetCurrentHalfResult(value);
            RefreshScorePageState();
            PublishScoreChanged(value);
        }
    }

    /// <summary>比分预览行列表。</summary>
    public ObservableCollection<ScorePreviewRow> ScorePreviewRows { get; } = [];

    #region 比分控制

    [RelayCommand]
    private void Escape4()
    {
        SetCurrentHalfResult(GameResult.Escape4);
    }

    [RelayCommand]
    private void Escape3()
    {
        SetCurrentHalfResult(GameResult.Escape3);
    }

    [RelayCommand]
    private void Tie()
    {
        SetCurrentHalfResult(GameResult.Tie);
    }

    [RelayCommand]
    private void Out3()
    {
        SetCurrentHalfResult(GameResult.Out3);
    }

    [RelayCommand]
    private void Out4()
    {
        SetCurrentHalfResult(GameResult.Out4);
    }

    [RelayCommand]
    private void ClearCurrentHalfScore()
    {
        if (IsFreeMode)
        {
            _matchScoreService.ClearFreeCurrentMinorScore();
            RefreshScorePageState();
            return;
        }

        _matchScoreService.ClearCurrentHalfResult();
        RefreshScorePageState();
    }

    [RelayCommand]
    private void Reset()
    {
        if (IsFreeMode)
        {
            _matchScoreService.ResetFreeScores();
            RefreshScorePageState();
            return;
        }

        foreach (var scoreGame in _matchScoreService.Current.Games)
        {
            ClearHalf(scoreGame.FirstHalf);
            ClearHalf(scoreGame.SecondHalf);
        }

        _matchScoreService.Recalculate();
        _matchScoreService.RefreshCurrentProgress();
        RefreshScorePageState();
    }

    #endregion

    #region 分数统计

    private void SetCurrentHalfResult(GameResult result)
    {
        if (IsFreeMode)
        {
            _matchScoreService.ApplyFreeResultPreset(result);
            RefreshScorePageState();
            PublishScoreChanged(result);
            return;
        }

        _matchScoreService.SetCurrentHalfResult(result);
        RefreshScorePageState();
        PublishScoreChanged(result);
    }

    [RelayCommand]
    private void SettleFreeMajorScore()
    {
        _matchScoreService.SettleFreeMajorScore();
        RefreshScorePageState();
    }

    [RelayCommand]
    private void OpenFreeInGameScoreEditor()
    {
        if (!IsFreeMode)
            return;

        if (IAppHost.Host?.Services.GetService<FreeInGameScoreEditorWindow>() is { } window)
        {
            window.Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive);
            window.ShowDialog();
        }
    }

    [RelayCommand]
    private void OpenFreeGlobalScoreEditor()
    {
        if (!IsFreeMode)
            return;

        if (IAppHost.Host?.Services.GetService<FreeGlobalScoreEditorWindow>() is { } window)
        {
            window.Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive);
            window.ShowDialog();
        }
    }

    private static void PublishScoreChanged(GameResult? result)
    {
        TutorialSignalPublisher.Publish(TutorialSignalIds.ScoreChanged, new { Result = result });
    }

    private void RefreshScorePageState()
    {
        _matchScoreService.Recalculate();
        _matchScoreService.RefreshCurrentProgress();
        RefreshScorePreviewRows();
        OnPropertyChanged(nameof(CurrentGame));
        OnPropertyChanged(nameof(HomeTeam));
        OnPropertyChanged(nameof(AwayTeam));
        RefreshCurrentHalfBindings();
    }

    private void RefreshScorePreviewRows()
    {
        ScorePreviewRows.Clear();

        if (IsFreeMode)
        {
            foreach (var scoreGame in CurrentGame.MatchScore.FreeScore.GlobalGames
                         .Where(game => ScoreGameVisibility.IsVisibleInBoMode(game.Key, _sharedDataService.IsBo3Mode)))
            {
                AddFreePreviewRow(scoreGame, scoreGame.FirstHalf);
                AddFreePreviewRow(scoreGame, scoreGame.SecondHalf);
            }

            return;
        }

        foreach (var scoreGame in CurrentGame.MatchScore.Games
                     .Where(game => ScoreGameVisibility.IsVisibleInBoMode(game.Key, _sharedDataService.IsBo3Mode)))
        {
            AddPreviewRow(scoreGame, scoreGame.FirstHalf);
            AddPreviewRow(scoreGame, scoreGame.SecondHalf);
        }
    }

    private void AddPreviewRow(ScoreGame scoreGame, ScoreHalf half)
    {
        var isCurrentProgress = half.Progress == CurrentGame.GameProgress;
        var hasResult = half.HasResult;

        ScorePreviewRows.Add(new ScorePreviewRow(
            FormatGameLabel(scoreGame.Key),
            FormatHalfLabel(half.HalfKind),
            half.Progress,
            FormatProgressLabel(scoreGame.Key, half.HalfKind),
            FormatResult(half.Result),
            FormatRecordedCamp(half, TeamType.HomeTeam),
            FormatRecordedCamp(half, TeamType.AwayTeam),
            half.HomeDisplayText,
            half.AwayDisplayText,
            CurrentGame.MatchScore.HomeTotalMinorScore.ToString(),
            CurrentGame.MatchScore.AwayTotalMinorScore.ToString(),
            hasResult,
            isCurrentProgress,
            isCurrentProgress
                ? Loc("ScorePreviewCurrent")
                : hasResult
                    ? Loc("ScorePreviewRecorded")
                    : Loc("ScorePreviewEmpty"),
            HomeTeam.Name,
            AwayTeam.Name));
    }

    private void AddFreePreviewRow(FreeGlobalScoreGame scoreGame, FreeGlobalScoreHalf half)
    {
        ScorePreviewRows.Add(new ScorePreviewRow(
            FormatGameLabel(scoreGame.Key),
            FormatHalfLabel(half.HalfKind),
            GameProgress.Free,
            FormatProgressLabel(scoreGame.Key, half.HalfKind),
            half.IsCompleted ? Loc("ScorePreviewManual") : "-",
            half.IsCompleted ? FormatCamp(half.HomeCamp) : "-",
            half.IsCompleted ? FormatCamp(half.AwayCamp) : "-",
            half.IsCompleted ? half.HomeMinorScore.ToString() : "-",
            half.IsCompleted ? half.AwayMinorScore.ToString() : "-",
            CurrentGame.MatchScore.FreeScore.Home.TotalMinorScore.ToString(),
            CurrentGame.MatchScore.FreeScore.Away.TotalMinorScore.ToString(),
            half.IsCompleted,
            false,
            half.IsCompleted ? Loc("ScorePreviewRecorded") : Loc("ScorePreviewEmpty"),
            HomeTeam.Name,
            AwayTeam.Name));
    }

    private void OnCurrentGameChanged(object? sender, EventArgs args)
    {
        SubscribeGame(_sharedDataService.CurrentGame);
        RefreshScorePageState();
    }

    private void OnIsBo3ModeChanged(object? sender, EventArgs args) => RefreshScorePageState();

    private void SubscribeGame(Game game)
    {
        if (_subscribedGame != null)
        {
            _subscribedGame.PropertyChanged -= OnCurrentGamePropertyChanged;
            _subscribedGame.TeamSwapped -= OnTeamSwapped;
        }

        if (_subscribedMatchScore != null)
        {
            _subscribedMatchScore.PropertyChanged -= OnMatchScorePropertyChanged;
        }

        _subscribedGame = game;
        _subscribedGame.PropertyChanged += OnCurrentGamePropertyChanged;
        _subscribedGame.TeamSwapped += OnTeamSwapped;

        _subscribedMatchScore = game.MatchScore;
        _subscribedMatchScore.PropertyChanged += OnMatchScorePropertyChanged;
    }

    private void OnCurrentGamePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(Game.GameProgress))
            return;

        RefreshScorePageState();
    }

    private void OnTeamSwapped(object? sender, EventArgs args) => RefreshScorePageState();

    private void OnMatchScorePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        RefreshScorePreviewRows();
        OnPropertyChanged(nameof(CurrentGame));
        OnPropertyChanged(nameof(HomeTeam));
        OnPropertyChanged(nameof(AwayTeam));
        RefreshCurrentHalfBindings();
    }

    private void RefreshCurrentHalfBindings()
    {
        OnPropertyChanged(nameof(IsFreeMode));
        OnPropertyChanged(nameof(SelectedCurrentHalfResult));
    }

    private static string FormatCamp(Camp camp) =>
        camp == Camp.Sur ? CommonLoc("Survivor") : CommonLoc("Hunter");

    private static void ClearHalf(ScoreHalf half)
    {
        half.Result = null;
        half.SurTeamTypeWhenRecorded = null;
        half.HunTeamTypeWhenRecorded = null;
    }

    private static string FormatGameLabel(ScoreGameKey key)
    {
        var formatKey = key.GameKind == ScoreGameKind.Overtime
            ? "ScorePreviewGameOvertimeFormat"
            : "ScorePreviewGameFormat";

        return string.Format(LocalizeDictionary.CurrentCulture, Loc(formatKey), key.GameNumber);
    }

    private static string FormatHalfLabel(ScoreHalfKind halfKind) =>
        halfKind == ScoreHalfKind.FirstHalf
            ? Loc("ScorePreviewFirstHalf")
            : Loc("ScorePreviewSecondHalf");

    private static string FormatProgressLabel(ScoreGameKey key, ScoreHalfKind halfKind) =>
        string.Format(
            LocalizeDictionary.CurrentCulture,
            Loc("ScorePreviewProgressFormat"),
            FormatGameLabel(key),
            FormatHalfLabel(halfKind));

    private static string FormatResult(GameResult? result) =>
        result switch
        {
            GameResult.Escape4 => Loc("FourEscape"),
            GameResult.Escape3 => Loc("ThreeEscape"),
            GameResult.Tie => Loc("Tie"),
            GameResult.Out3 => Loc("ThreeEliminate"),
            GameResult.Out4 => Loc("FourEliminate"),
            _ => "-"
        };

    private static string FormatRecordedCamp(ScoreHalf half, TeamType teamType)
    {
        if (half.SurTeamTypeWhenRecorded == teamType)
            return CommonLoc("Survivor");

        if (half.HunTeamTypeWhenRecorded == teamType)
            return CommonLoc("Hunter");

        return "-";
    }

    private static string Loc(string key) =>
        I18nHelper.GetLocalizedString(AppI18nDictionaries.Score, key, LocalizeDictionary.CurrentCulture);

    private static string CommonLoc(string key) =>
        I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, key, LocalizeDictionary.CurrentCulture);

    private void OnLocalizeDictionaryPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName)
            || args.PropertyName == nameof(LocalizeDictionary.Culture)
            || args.PropertyName == nameof(LocalizeDictionary.CurrentCulture))
        {
            RefreshScorePreviewRows();
        }
    }

    #endregion
}
