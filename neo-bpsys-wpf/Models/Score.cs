using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;
using neo_bpsys_wpf.Abstractions.ViewModels;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 比分类, 用于展示比分
/// </summary>
public partial class Score : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    [NotifyPropertyChangedFor(nameof(ScorePreviewOnBack))]
    private int _win;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    [NotifyPropertyChangedFor(nameof(ScorePreviewOnBack))]
    private int _tie;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    [NotifyPropertyChangedFor(nameof(ScorePreviewOnBack))]
    private int _minorPoints;

    [JsonIgnore]
    public string MajorPointsOnFront => $"W{Win}  D{Tie}";

    [JsonIgnore]
    public string ScorePreviewOnBack => $"W:{Win} D:{Tie} 小比分:{MinorPoints}";
}