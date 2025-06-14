using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 比分类, 用于展示比分
/// </summary>
public partial class Score : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    [NotifyPropertyChangedFor(nameof(ScorePreviewOnBack))]
    private int _win = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    [NotifyPropertyChangedFor(nameof(ScorePreviewOnBack))]
    private int _tie = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    [NotifyPropertyChangedFor(nameof(ScorePreviewOnBack))]
    private int _minorPoints = 0;

    [JsonIgnore]
    public string MajorPointsOnFront => $"W{Win}  D{Tie}";

    [JsonIgnore]
    public string ScorePreviewOnBack => $"W:{Win} D:{Tie} 小比分:{MinorPoints}";
}