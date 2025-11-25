using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 比分类, 用于展示比分
/// </summary>
public partial class Score : ViewModelBase
{
    /// <summary>
    /// 大比分--胜
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    private int _win;
    
    /// <summary>
    /// 大比分--平
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    private int _tie;
    
    /// <summary>
    /// 小比分
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    private int _minorPoints;
    
    /// <summary>
    /// 前台显示的格式化的大比分
    /// </summary>
    [JsonIgnore] public string MajorPointsOnFront => $"W{Win}  D{Tie}";

    internal void Reset()
    {
        Win = 0;
        Tie = 0;
        MinorPoints = 0;
    }
}