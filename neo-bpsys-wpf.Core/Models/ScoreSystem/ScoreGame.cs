using neo_bpsys_wpf.Core.Abstractions;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// Score System v2 中的一个计分 Game，例如 Game 1、Game 3 Overtime 或 Game 5 Overtime。
/// </summary>
/// <remarks>
/// 一个 <see cref="ScoreGame"/> 由上下两个 <see cref="ScoreHalf"/> 组成。只有两半都有结果且主客队小比分都能派生时，
/// 此 Game 才参与大比分胜负计算。
/// </remarks>
public partial class ScoreGame : ObservableObjectBase
{
    private ScoreGameKey _key;
    private ScoreHalf _firstHalf;
    private ScoreHalf _secondHalf;

    /// <summary>
    /// 创建一个计分 Game。
    /// </summary>
    /// <param name="key">稳定定位该 Game 的 key。</param>
    /// <param name="firstHalf">第一半比分。</param>
    /// <param name="secondHalf">第二半比分。</param>
    [JsonConstructor]
    public ScoreGame(ScoreGameKey key, ScoreHalf firstHalf, ScoreHalf secondHalf)
    {
        _key = key;
        _firstHalf = firstHalf;
        _secondHalf = secondHalf;

        SubscribeHalf(_firstHalf);
        SubscribeHalf(_secondHalf);
    }

    /// <summary>
    /// 稳定定位该比分单元的 key。
    /// </summary>
    public ScoreGameKey Key
    {
        get => _key;
        set => SetPropertyWithDerivedRefresh(ref _key, value);
    }

    /// <summary>
    /// 第一半比分。
    /// </summary>
    public ScoreHalf FirstHalf
    {
        get => _firstHalf;
        set
        {
            if (_firstHalf == value)
                return;

            UnsubscribeHalf(_firstHalf);
            SetPropertyWithDerivedRefresh(ref _firstHalf, value);
            SubscribeHalf(_firstHalf);
        }
    }

    /// <summary>
    /// 第二半比分。
    /// </summary>
    public ScoreHalf SecondHalf
    {
        get => _secondHalf;
        set
        {
            if (_secondHalf == value)
                return;

            UnsubscribeHalf(_secondHalf);
            SetPropertyWithDerivedRefresh(ref _secondHalf, value);
            SubscribeHalf(_secondHalf);
        }
    }

    /// <summary>
    /// 两半是否都已记录并能派生主客队小比分。
    /// </summary>
    [JsonIgnore]
    public bool IsComplete =>
        FirstHalf.HasResult &&
        SecondHalf.HasResult &&
        FirstHalf.HomeMinorScore.HasValue &&
        FirstHalf.AwayMinorScore.HasValue &&
        SecondHalf.HomeMinorScore.HasValue &&
        SecondHalf.AwayMinorScore.HasValue;

    /// <summary>
    /// 该 Game 完整时的主队小比分；未完整时为 <see langword="null"/>。
    /// </summary>
    [JsonIgnore]
    public int? HomeMinorScore =>
        IsComplete ? FirstHalf.HomeMinorScore + SecondHalf.HomeMinorScore : null;

    /// <summary>
    /// 该 Game 完整时的客队小比分；未完整时为 <see langword="null"/>。
    /// </summary>
    [JsonIgnore]
    public int? AwayMinorScore =>
        IsComplete ? FirstHalf.AwayMinorScore + SecondHalf.AwayMinorScore : null;

    /// <summary>
    /// 该 Game 完整时派生出的大比分胜负结果。
    /// </summary>
    [JsonIgnore]
    public ScoreGameMajorResult? MajorResult
    {
        get
        {
            if (!IsComplete || !HomeMinorScore.HasValue || !AwayMinorScore.HasValue)
                return null;

            if (HomeMinorScore > AwayMinorScore)
                return ScoreGameMajorResult.HomeWin;

            if (AwayMinorScore > HomeMinorScore)
                return ScoreGameMajorResult.AwayWin;

            return ScoreGameMajorResult.Tie;
        }
    }

    internal IEnumerable<ScoreHalf> Halves
    {
        get
        {
            yield return FirstHalf;
            yield return SecondHalf;
        }
    }

    internal void NotifyDerivedPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(HomeMinorScore));
        OnPropertyChanged(nameof(AwayMinorScore));
        OnPropertyChanged(nameof(MajorResult));
    }

    private bool SetPropertyWithDerivedRefresh<T>(ref T field, T value)
    {
        if (!SetProperty(ref field, value))
            return false;

        NotifyDerivedPropertiesChanged();
        return true;
    }

    private void SubscribeHalf(ScoreHalf half) => half.PropertyChanged += OnHalfPropertyChanged;

    private void UnsubscribeHalf(ScoreHalf half) => half.PropertyChanged -= OnHalfPropertyChanged;

    private void OnHalfPropertyChanged(object? sender, PropertyChangedEventArgs args) => NotifyDerivedPropertiesChanged();
}
