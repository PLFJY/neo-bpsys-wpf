using neo_bpsys_wpf.Core.Abstractions;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

public partial class ScoreGame : ObservableObjectBase
{
    private ScoreGameKey _key;
    private ScoreHalf _firstHalf;
    private ScoreHalf _secondHalf;

    [JsonConstructor]
    public ScoreGame(ScoreGameKey key, ScoreHalf firstHalf, ScoreHalf secondHalf)
    {
        _key = key;
        _firstHalf = firstHalf;
        _secondHalf = secondHalf;

        SubscribeHalf(_firstHalf);
        SubscribeHalf(_secondHalf);
    }

    public ScoreGameKey Key
    {
        get => _key;
        set => SetPropertyWithDerivedRefresh(ref _key, value);
    }

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

    [JsonIgnore]
    public bool IsComplete =>
        FirstHalf.HasResult &&
        SecondHalf.HasResult &&
        FirstHalf.HomeMinorScore.HasValue &&
        FirstHalf.AwayMinorScore.HasValue &&
        SecondHalf.HomeMinorScore.HasValue &&
        SecondHalf.AwayMinorScore.HasValue;

    [JsonIgnore]
    public int? HomeMinorScore =>
        IsComplete ? FirstHalf.HomeMinorScore + SecondHalf.HomeMinorScore : null;

    [JsonIgnore]
    public int? AwayMinorScore =>
        IsComplete ? FirstHalf.AwayMinorScore + SecondHalf.AwayMinorScore : null;

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
