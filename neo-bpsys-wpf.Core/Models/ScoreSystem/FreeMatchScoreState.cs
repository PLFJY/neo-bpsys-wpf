using neo_bpsys_wpf.Core.Abstractions;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// 与 Score System V2 完全隔离的自由对局比分状态。
/// </summary>
public sealed class FreeMatchScoreState : ObservableObjectBase
{
    private readonly FreeTeamScoreState _home;
    private readonly FreeTeamScoreState _away;
    private readonly ObservableCollection<FreeGlobalScoreGame> _globalGames;

    /// <summary>初始化自由对局比分状态。</summary>
    /// <param name="home">主队自由比分。</param>
    /// <param name="away">客队自由比分。</param>
    /// <param name="globalGames">自由 ScoreGlobal 记录。</param>
    [JsonConstructor]
    public FreeMatchScoreState(
        FreeTeamScoreState? home = null,
        FreeTeamScoreState? away = null,
        ObservableCollection<FreeGlobalScoreGame>? globalGames = null)
    {
        _home = home ?? new FreeTeamScoreState();
        _away = away ?? new FreeTeamScoreState();
        _globalGames = globalGames ?? CreateDefaultGames();
        _home.PropertyChanged += OnChildPropertyChanged;
        _away.PropertyChanged += OnChildPropertyChanged;
        SubscribeGames(_globalGames);
    }

    /// <summary>获取主队自由比分。</summary>
    public FreeTeamScoreState Home => _home;

    /// <summary>获取客队自由比分。</summary>
    public FreeTeamScoreState Away => _away;

    /// <summary>获取自由 ScoreGlobal 的全部稳定比分单元。</summary>
    public ObservableCollection<FreeGlobalScoreGame> GlobalGames => _globalGames;

    /// <summary>按稳定键和半场位置取得自由全局比分。</summary>
    /// <param name="key">稳定 Game 键。</param>
    /// <param name="halfKind">半场位置。</param>
    /// <returns>对应半场；找不到时为 <see langword="null"/>。</returns>
    public FreeGlobalScoreHalf? GetGlobalHalf(ScoreGameKey key, ScoreHalfKind halfKind) =>
        GlobalGames.FirstOrDefault(game => game.Key == key)?.GetHalf(halfKind);

    /// <summary>重置全部自由比分，包括 ScoreGlobal 记录。</summary>
    public void Reset()
    {
        Home.Reset();
        Away.Reset();
        foreach (var game in GlobalGames)
        {
            game.FirstHalf.Reset();
            game.SecondHalf.Reset();
        }
    }

    /// <summary>创建当前自由比分状态的独立副本。</summary>
    /// <returns>独立副本。</returns>
    public FreeMatchScoreState Clone() => new(
        Home.Clone(),
        Away.Clone(),
        new ObservableCollection<FreeGlobalScoreGame>(GlobalGames.Select(game => game.Clone())));

    private static ObservableCollection<FreeGlobalScoreGame> CreateDefaultGames() =>
    [
        CreateGame(1, ScoreGameKind.Normal),
        CreateGame(2, ScoreGameKind.Normal),
        CreateGame(3, ScoreGameKind.Normal),
        CreateGame(3, ScoreGameKind.Overtime),
        CreateGame(4, ScoreGameKind.Normal),
        CreateGame(5, ScoreGameKind.Normal),
        CreateGame(5, ScoreGameKind.Overtime)
    ];

    private static FreeGlobalScoreGame CreateGame(int number, ScoreGameKind kind) => new(
        new ScoreGameKey(number, kind),
        new FreeGlobalScoreHalf(ScoreHalfKind.FirstHalf),
        new FreeGlobalScoreHalf(ScoreHalfKind.SecondHalf));

    private void SubscribeGames(ObservableCollection<FreeGlobalScoreGame> games)
    {
        games.CollectionChanged += OnGamesCollectionChanged;
        foreach (var game in games)
            game.PropertyChanged += OnChildPropertyChanged;
    }

    private void OnGamesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems is not null)
        {
            foreach (FreeGlobalScoreGame game in args.OldItems)
                game.PropertyChanged -= OnChildPropertyChanged;
        }

        if (args.NewItems is not null)
        {
            foreach (FreeGlobalScoreGame game in args.NewItems)
                game.PropertyChanged += OnChildPropertyChanged;
        }

        OnPropertyChanged(nameof(GlobalGames));
    }

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        OnPropertyChanged(sender == Home ? nameof(Home) : sender == Away ? nameof(Away) : nameof(GlobalGames));
}
