using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

public partial class MatchScoreState : ObservableObjectBase
{
    private ObservableCollection<ScoreGame> _games;
    private int _homeMajorWin;
    private int _homeMajorTie;
    private int _awayMajorWin;
    private int _awayMajorTie;
    private string _homeMajorText = "W0  D0";
    private string _awayMajorText = "W0  D0";
    private int _homeTotalMinorScore;
    private int _awayTotalMinorScore;
    private string _currentSurTeamPreHalfMinorScoreText = "0";
    private string _currentHunTeamPreHalfMinorScoreText = "0";
    private string _currentSurTeamMajorText = "W0  D0";
    private string _currentHunTeamMajorText = "W0  D0";
    private GameProgress _currentDisplayProgress = GameProgress.Free;
    private TeamType _currentDisplaySurTeamType = TeamType.HomeTeam;
    private TeamType _currentDisplayHunTeamType = TeamType.AwayTeam;
    private bool _currentDisplayIsBo3Mode;

    [JsonConstructor]
    public MatchScoreState(ObservableCollection<ScoreGame>? games = null)
    {
        _games = games ?? CreateDefaultGames();
        SubscribeGames(_games);
        Recalculate();
    }

    public ObservableCollection<ScoreGame> Games
    {
        get => _games;
        set
        {
            if (_games == value)
                return;

            UnsubscribeGames(_games);
            SetProperty(ref _games, value);
            SubscribeGames(_games);
            Recalculate();
        }
    }

    [JsonIgnore]
    public int HomeMajorWin
    {
        get => _homeMajorWin;
        private set => SetProperty(ref _homeMajorWin, value);
    }

    [JsonIgnore]
    public int HomeMajorTie
    {
        get => _homeMajorTie;
        private set => SetProperty(ref _homeMajorTie, value);
    }

    [JsonIgnore]
    public int AwayMajorWin
    {
        get => _awayMajorWin;
        private set => SetProperty(ref _awayMajorWin, value);
    }

    [JsonIgnore]
    public int AwayMajorTie
    {
        get => _awayMajorTie;
        private set => SetProperty(ref _awayMajorTie, value);
    }

    [JsonIgnore]
    public string HomeMajorText
    {
        get => _homeMajorText;
        private set => SetProperty(ref _homeMajorText, value);
    }

    [JsonIgnore]
    public string AwayMajorText
    {
        get => _awayMajorText;
        private set => SetProperty(ref _awayMajorText, value);
    }

    [JsonIgnore]
    public int HomeTotalMinorScore
    {
        get => _homeTotalMinorScore;
        private set => SetProperty(ref _homeTotalMinorScore, value);
    }

    [JsonIgnore]
    public int AwayTotalMinorScore
    {
        get => _awayTotalMinorScore;
        private set => SetProperty(ref _awayTotalMinorScore, value);
    }

    [JsonIgnore]
    public string CurrentSurTeamPreHalfMinorScoreText
    {
        get => _currentSurTeamPreHalfMinorScoreText;
        private set => SetProperty(ref _currentSurTeamPreHalfMinorScoreText, value);
    }

    [JsonIgnore]
    public string CurrentHunTeamPreHalfMinorScoreText
    {
        get => _currentHunTeamPreHalfMinorScoreText;
        private set => SetProperty(ref _currentHunTeamPreHalfMinorScoreText, value);
    }

    [JsonIgnore]
    public string CurrentSurTeamMajorText
    {
        get => _currentSurTeamMajorText;
        private set => SetProperty(ref _currentSurTeamMajorText, value);
    }

    [JsonIgnore]
    public string CurrentHunTeamMajorText
    {
        get => _currentHunTeamMajorText;
        private set => SetProperty(ref _currentHunTeamMajorText, value);
    }

    public static MatchScoreState CreateDefault() => new(CreateDefaultGames());

    public MatchScoreState Clone()
    {
        var games = new ObservableCollection<ScoreGame>(
            Games.Select(game => new ScoreGame(
                game.Key,
                CloneHalf(game.FirstHalf),
                CloneHalf(game.SecondHalf))));

        var clone = new MatchScoreState(games);
        clone._currentDisplayProgress = _currentDisplayProgress;
        clone._currentDisplaySurTeamType = _currentDisplaySurTeamType;
        clone._currentDisplayHunTeamType = _currentDisplayHunTeamType;
        clone._currentDisplayIsBo3Mode = _currentDisplayIsBo3Mode;
        clone.UpdateCurrentDisplay();
        return clone;
    }

    public ScoreGame? GetGame(GameProgress progress) => GetGame(progress, isBo3Mode: false);

    public ScoreGame? GetGame(GameProgress progress, bool isBo3Mode)
    {
        var key = ResolveScoreGameKey(progress, isBo3Mode);
        return key == null ? null : Games.FirstOrDefault(game => game.Key == key.Value);
    }

    public ScoreHalf? GetHalf(GameProgress progress) => GetHalf(progress, isBo3Mode: false);

    public ScoreHalf? GetHalf(GameProgress progress, bool isBo3Mode)
    {
        var game = GetGame(progress, isBo3Mode);
        if (game == null)
            return null;

        var halfKind = ResolveHalfKind(progress);
        return halfKind switch
        {
            ScoreHalfKind.FirstHalf => game.FirstHalf,
            ScoreHalfKind.SecondHalf => game.SecondHalf,
            _ => null
        };
    }

    public void Recalculate()
    {
        var homeMajorWin = 0;
        var homeMajorTie = 0;
        var awayMajorWin = 0;
        var awayMajorTie = 0;
        var homeTotalMinorScore = 0;
        var awayTotalMinorScore = 0;

        foreach (var game in Games)
        {
            switch (game.MajorResult)
            {
                case ScoreGameMajorResult.HomeWin:
                    homeMajorWin++;
                    break;
                case ScoreGameMajorResult.AwayWin:
                    awayMajorWin++;
                    break;
                case ScoreGameMajorResult.Tie:
                    homeMajorTie++;
                    awayMajorTie++;
                    break;
            }

            foreach (var half in game.Halves)
            {
                homeTotalMinorScore += half.HomeMinorScore ?? 0;
                awayTotalMinorScore += half.AwayMinorScore ?? 0;
            }
        }

        HomeMajorWin = homeMajorWin;
        HomeMajorTie = homeMajorTie;
        AwayMajorWin = awayMajorWin;
        AwayMajorTie = awayMajorTie;
        HomeMajorText = FormatMajorText(HomeMajorWin, HomeMajorTie);
        AwayMajorText = FormatMajorText(AwayMajorWin, AwayMajorTie);
        HomeTotalMinorScore = homeTotalMinorScore;
        AwayTotalMinorScore = awayTotalMinorScore;
        UpdateCurrentDisplay();
    }

    public void RefreshCurrentDisplay(GameProgress progress, TeamType currentSurTeamType, TeamType currentHunTeamType)
    {
        RefreshCurrentDisplay(progress, currentSurTeamType, currentHunTeamType, isBo3Mode: false);
    }

    public void RefreshCurrentDisplay(
        GameProgress progress,
        TeamType currentSurTeamType,
        TeamType currentHunTeamType,
        bool isBo3Mode)
    {
        _currentDisplayProgress = progress;
        _currentDisplaySurTeamType = currentSurTeamType;
        _currentDisplayHunTeamType = currentHunTeamType;
        _currentDisplayIsBo3Mode = isBo3Mode;
        UpdateCurrentDisplay();
    }

    private void UpdateCurrentDisplay()
    {
        CurrentSurTeamMajorText = GetMajorText(_currentDisplaySurTeamType);
        CurrentHunTeamMajorText = GetMajorText(_currentDisplayHunTeamType);

        var currentGame = GetGame(_currentDisplayProgress, _currentDisplayIsBo3Mode);
        if (currentGame == null || ResolveHalfKind(_currentDisplayProgress) != ScoreHalfKind.SecondHalf)
        {
            CurrentSurTeamPreHalfMinorScoreText = "0";
            CurrentHunTeamPreHalfMinorScoreText = "0";
            return;
        }

        CurrentSurTeamPreHalfMinorScoreText =
            FormatMinorScore(GetTeamMinorScore(currentGame.FirstHalf, _currentDisplaySurTeamType, fallbackToZero: true));
        CurrentHunTeamPreHalfMinorScoreText =
            FormatMinorScore(GetTeamMinorScore(currentGame.FirstHalf, _currentDisplayHunTeamType, fallbackToZero: true));
    }

    private static ScoreHalf CloneHalf(ScoreHalf half) =>
        new(
            half.Progress,
            half.HalfKind,
            half.Result,
            half.SurTeamTypeWhenRecorded,
            half.HunTeamTypeWhenRecorded);

    private static ObservableCollection<ScoreGame> CreateDefaultGames() =>
    [
        CreateScoreGame(1, ScoreGameKind.Normal, GameProgress.Game1FirstHalf, GameProgress.Game1SecondHalf),
        CreateScoreGame(2, ScoreGameKind.Normal, GameProgress.Game2FirstHalf, GameProgress.Game2SecondHalf),
        CreateScoreGame(3, ScoreGameKind.Normal, GameProgress.Game3FirstHalf, GameProgress.Game3SecondHalf),
        CreateScoreGame(3, ScoreGameKind.Overtime, GameProgress.Game3OvertimeFirstHalf,
            GameProgress.Game3OvertimeSecondHalf),
        CreateScoreGame(4, ScoreGameKind.Normal, GameProgress.Game4FirstHalf, GameProgress.Game4SecondHalf),
        CreateScoreGame(5, ScoreGameKind.Normal, GameProgress.Game5FirstHalf, GameProgress.Game5SecondHalf),
        CreateScoreGame(5, ScoreGameKind.Overtime, GameProgress.Game5OvertimeFirstHalf,
            GameProgress.Game5OvertimeSecondHalf)
    ];

    private static ScoreGame CreateScoreGame(
        int gameNumber,
        ScoreGameKind gameKind,
        GameProgress firstHalfProgress,
        GameProgress secondHalfProgress) =>
        new(
            new ScoreGameKey(gameNumber, gameKind),
            new ScoreHalf(firstHalfProgress, ScoreHalfKind.FirstHalf),
            new ScoreHalf(secondHalfProgress, ScoreHalfKind.SecondHalf));

    private static ScoreGameKey? ResolveScoreGameKey(GameProgress progress, bool isBo3Mode)
    {
        return (int)progress switch
        {
            0 or 1 => new ScoreGameKey(1, ScoreGameKind.Normal),
            2 or 3 => new ScoreGameKey(2, ScoreGameKind.Normal),
            4 or 5 => new ScoreGameKey(3, ScoreGameKind.Normal),
            6 or 7 => isBo3Mode
                ? new ScoreGameKey(3, ScoreGameKind.Overtime)
                : new ScoreGameKey(4, ScoreGameKind.Normal),
            8 or 9 => new ScoreGameKey(5, ScoreGameKind.Normal),
            10 or 11 => new ScoreGameKey(5, ScoreGameKind.Overtime),
            _ => null
        };
    }

    private static ScoreHalfKind? ResolveHalfKind(GameProgress progress)
    {
        return (int)progress switch
        {
            0 or 2 or 4 or 6 or 8 or 10 => ScoreHalfKind.FirstHalf,
            1 or 3 or 5 or 7 or 9 or 11 => ScoreHalfKind.SecondHalf,
            _ => null
        };
    }

    private string GetMajorText(TeamType teamType) =>
        teamType == TeamType.HomeTeam ? HomeMajorText : AwayMajorText;

    private static int? GetTeamMinorScore(ScoreHalf half, TeamType teamType, bool fallbackToZero)
    {
        var score = teamType switch
        {
            TeamType.HomeTeam => half.HomeMinorScore,
            TeamType.AwayTeam => half.AwayMinorScore,
            _ => null
        };

        return score ?? (fallbackToZero ? 0 : null);
    }

    private static string FormatMajorText(int win, int tie) => $"W{win}  D{tie}";

    private static string FormatMinorScore(int? score) =>
        score?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private void SubscribeGames(ObservableCollection<ScoreGame> games)
    {
        games.CollectionChanged += OnGamesCollectionChanged;
        foreach (var game in games)
        {
            game.PropertyChanged += OnScoreGamePropertyChanged;
        }
    }

    private void UnsubscribeGames(ObservableCollection<ScoreGame> games)
    {
        games.CollectionChanged -= OnGamesCollectionChanged;
        foreach (var game in games)
        {
            game.PropertyChanged -= OnScoreGamePropertyChanged;
        }
    }

    private void OnGamesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems != null)
        {
            foreach (ScoreGame game in args.OldItems)
            {
                game.PropertyChanged -= OnScoreGamePropertyChanged;
            }
        }

        if (args.NewItems != null)
        {
            foreach (ScoreGame game in args.NewItems)
            {
                game.PropertyChanged += OnScoreGamePropertyChanged;
            }
        }

        Recalculate();
    }

    private void OnScoreGamePropertyChanged(object? sender, PropertyChangedEventArgs args) => Recalculate();
}
