using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Models;

public partial class Score : ObservableObject
{
    [ObservableProperty]
    public int _win = 0;

    [ObservableProperty]
    public int _lose = 0;

    [ObservableProperty]
    public int _tie = 0;

    [ObservableProperty]
    public int _minorPoints = 0;
}
