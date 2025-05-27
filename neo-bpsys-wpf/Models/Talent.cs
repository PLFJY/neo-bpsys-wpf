using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Models;

public partial class Talent : ObservableObject
{
    //Sur
    [ObservableProperty]
    private bool _borrowedTime = false;
    [ObservableProperty]
    private bool _flywheelEffect = false;
    [ObservableProperty]
    private bool _kneeJerkReflex = false;
    [ObservableProperty]
    private bool _tideTurner = false;

    //Hun
    [ObservableProperty]
    private bool _confinedSpace = false;
    [ObservableProperty]
    private bool _detention = false;
    [ObservableProperty]
    private bool _insolence = false;
    [ObservableProperty]
    private bool _trumpCard = false;
}
