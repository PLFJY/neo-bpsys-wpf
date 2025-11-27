using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys.Core.Models;

public partial class Score : ObservableObject
{
    [ObservableProperty] private int _majorPoints;
    [ObservableProperty] private int _minorPoints;
    public string MajorPointsOnFront => $"W{MajorPoints} D{MinorPoints}";
}
