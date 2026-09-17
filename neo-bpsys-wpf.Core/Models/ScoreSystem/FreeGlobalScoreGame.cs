using neo_bpsys_wpf.Core.Abstractions;
using System.ComponentModel;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// 自由对局 ScoreGlobal 中一个包含上下半场的计分 Game。
/// </summary>
public sealed class FreeGlobalScoreGame : ObservableObjectBase
{
    /// <summary>初始化自由全局比分 Game。</summary>
    /// <param name="key">稳定 Game 键。</param>
    /// <param name="firstHalf">上半场状态。</param>
    /// <param name="secondHalf">下半场状态。</param>
    public FreeGlobalScoreGame(
        ScoreGameKey key,
        FreeGlobalScoreHalf firstHalf,
        FreeGlobalScoreHalf secondHalf)
    {
        Key = key;
        FirstHalf = firstHalf;
        SecondHalf = secondHalf;
        FirstHalf.PropertyChanged += OnHalfPropertyChanged;
        SecondHalf.PropertyChanged += OnHalfPropertyChanged;
    }

    /// <summary>获取稳定 Game 键。</summary>
    public ScoreGameKey Key { get; }

    /// <summary>获取上半场状态。</summary>
    public FreeGlobalScoreHalf FirstHalf { get; }

    /// <summary>获取下半场状态。</summary>
    public FreeGlobalScoreHalf SecondHalf { get; }

    /// <summary>按半场位置取得状态。</summary>
    /// <param name="halfKind">半场位置。</param>
    /// <returns>对应半场。</returns>
    public FreeGlobalScoreHalf GetHalf(ScoreHalfKind halfKind) =>
        halfKind == ScoreHalfKind.FirstHalf ? FirstHalf : SecondHalf;

    /// <summary>创建当前 Game 的独立副本。</summary>
    /// <returns>独立副本。</returns>
    public FreeGlobalScoreGame Clone() => new(Key, FirstHalf.Clone(), SecondHalf.Clone());

    private void OnHalfPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        OnPropertyChanged(nameof(FirstHalf));
}
