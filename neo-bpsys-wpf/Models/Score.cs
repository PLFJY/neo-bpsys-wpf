using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 比分类, 用于展示比分
/// </summary>
public partial class Score : ObservableObject
{
    private int _win = 0;
    public int Win
    {
        get => _win;
        set
        {
            if (_win != value)
            {
                _win = value;
                OnPropertyChanged(nameof(Win));
                OnPropertyChanged(nameof(MajorPointsOnFront));
                OnPropertyChanged(nameof(ScorePreviewOnBack));
            }
        }
    }

    private int _tie = 0;
    public int Tie
    {
        get => _tie;
        set
        {
            if (_tie != value)
            {
                _tie = value;
                OnPropertyChanged(nameof(Tie));
                OnPropertyChanged(nameof(MajorPointsOnFront));
                OnPropertyChanged(nameof(ScorePreviewOnBack));
            }
        }
    }

    private int _minorPoints = 0;
    public int MinorPoints
    {
        get => _minorPoints;
        set
        {
            if (_minorPoints != value)
            {
                _minorPoints = value;
                OnPropertyChanged(nameof(MinorPoints));
                OnPropertyChanged(nameof(ScorePreviewOnBack));
            }
        }
    }

    [JsonIgnore]
    public string MajorPointsOnFront
    {
        get => $"W{Win}  D{Tie}";
    }

    [JsonIgnore]
    public string ScorePreviewOnBack
    {
        get => $"W:{Win} D:{Tie} 小比分:{MinorPoints}";
    }

}