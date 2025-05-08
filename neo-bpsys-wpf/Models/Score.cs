using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Models;

public partial class Score : ObservableObject
{
    [ObservableProperty]
    private int _win = 0;

    [ObservableProperty]
    private int _lose = 0;

    [ObservableProperty]
    private int _tie = 0;

    [ObservableProperty]
    private int _minorPoints = 0;
}
