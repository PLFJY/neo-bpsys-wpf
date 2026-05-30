using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Locales;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

public sealed class ScorePreviewRow
{
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

    public string GameLabel { get; }
    public string HalfLabel { get; }
    public GameProgress Progress { get; }
    public string ProgressText { get; }
    public string ResultText { get; }
    public string HomeCampText { get; }
    public string AwayCampText { get; }
    public string HomeMinorScoreText { get; }
    public string AwayMinorScoreText { get; }
    public bool HasResult { get; }
    public bool IsCurrentProgress { get; }
    public string RowStatusText { get; }
    public string HomeTeamName { get; }
    public string AwayTeamName { get; }
}

public partial class ScorePageViewModel : ViewModelBase
{
#pragma warning disable CS8618
    public ScorePageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly IMatchScoreService _matchScoreService;
    private Game? _subscribedGame;
    private MatchScoreState? _subscribedMatchScore;

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

    public Game CurrentGame => _sharedDataService.CurrentGame;
    public Team HomeTeam => _sharedDataService.HomeTeam;
    public Team AwayTeam => _sharedDataService.AwayTeam;
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
        _matchScoreService.SyncLegacyTeamScoreMirror();
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
    }

    private void RefreshScorePageState()
    {
        _matchScoreService.Recalculate();
        _matchScoreService.RefreshCurrentProgress();
        RefreshScorePreviewRows();
        OnPropertyChanged(nameof(CurrentGame));
        OnPropertyChanged(nameof(HomeTeam));
        OnPropertyChanged(nameof(AwayTeam));
    }

    private void RefreshScorePreviewRows()
    {
        ScorePreviewRows.Clear();

        foreach (var scoreGame in CurrentGame.MatchScore.Games
                     .Where(game => GlobalScoreRowDisplay.IsVisibleInBoMode(game.Key, _sharedDataService.IsBo3Mode)))
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

    private void OnMatchScorePropertyChanged(object? sender, PropertyChangedEventArgs args) => RefreshScorePreviewRows();

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
        Lang.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    #endregion
}
