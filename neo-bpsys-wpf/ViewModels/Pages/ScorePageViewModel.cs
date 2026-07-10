using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Tutorial;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Team = neo_bpsys_wpf.Core.Models.Team;

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
        SubscribeGame(_sharedDataService.CurrentGame);
        RefreshScorePageState();
    }

    /// <summary>获取当前比赛数据。</summary>
    public Game CurrentGame => _sharedDataService.CurrentGame;

    /// <summary>获取主队数据。</summary>
    public Team HomeTeam => _sharedDataService.HomeTeam;

    /// <summary>获取客队数据。</summary>
    public Team AwayTeam => _sharedDataService.AwayTeam;

    /// <summary>获取比分控件是否可用。</summary>
    public bool IsScoreControlEnabled => _sharedDataService.CurrentGame.GameProgress > GameProgress.Free;

    /// <summary>
    /// 获取或设置当前半场的比赛结果。
    /// </summary>
    public GameResult? SelectedCurrentHalfResult
    {
        get => _matchScoreService.CurrentHalf?.Result;
        set
        {
            if (!IsScoreControlEnabled || _matchScoreService.CurrentHalf?.Result == value)
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
        if (_sharedDataService.CurrentGame.GameProgress <= GameProgress.Free)
            return;

        _matchScoreService.ClearCurrentHalfResult();
        RefreshScorePageState();
    }

    [RelayCommand]
    private void Reset()
    {
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
        if (_sharedDataService.CurrentGame.GameProgress <= GameProgress.Free)
            return;

        _matchScoreService.SetCurrentHalfResult(result);
        RefreshScorePageState();
        PublishScoreChanged(result);
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
        RefreshCurrentHalfBindings();
    }

    private void RefreshCurrentHalfBindings()
    {
        OnPropertyChanged(nameof(IsScoreControlEnabled));
        OnPropertyChanged(nameof(SelectedCurrentHalfResult));
    }

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

        return string.Format(CultureInfo.CurrentUICulture, Loc(formatKey), key.GameNumber);
    }

    private static string FormatHalfLabel(ScoreHalfKind halfKind) =>
        halfKind == ScoreHalfKind.FirstHalf
            ? Loc("ScorePreviewFirstHalf")
            : Loc("ScorePreviewSecondHalf");

    private static string FormatProgressLabel(ScoreGameKey key, ScoreHalfKind halfKind) =>
        string.Format(
            CultureInfo.CurrentUICulture,
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
            return Loc("Survivor");

        if (half.HunTeamTypeWhenRecorded == teamType)
            return Loc("Hunter");

        return "-";
    }

    private static string Loc(string key) =>
        I18nHelper.GetLocalizedString(AppI18nDictionaries.Score, key, CultureInfo.CurrentUICulture);

    #endregion
}
