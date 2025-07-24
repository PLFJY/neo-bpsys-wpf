using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Models;

public partial class Talent : ObservableObject
{
    //Sur
    [ObservableProperty]
    private bool _borrowedTime;
    [ObservableProperty]
    private bool _flywheelEffect;
    [ObservableProperty]
    private bool _kneeJerkReflex;
    [ObservableProperty]
    private bool _tideTurner;

    //Hun
    [ObservableProperty]
    private bool _confinedSpace;
    [ObservableProperty]
    private bool _detention;
    [ObservableProperty]
    private bool _insolence;
    [ObservableProperty]
    private bool _trumpCard;
}
