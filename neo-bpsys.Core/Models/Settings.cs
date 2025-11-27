using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys.Core.Models;

public partial class Settings : ObservableObject
{
    [ObservableProperty] private ScoreWindowSettings _scoreWindowSettings = new();
}

public partial class ScoreWindowSettings : ObservableObject
{
    [ObservableProperty] private string? _surScoreBgImageUri;
    [ObservableProperty] private string? _hunScoreBgImageUri;
    [ObservableProperty] private string? _globalScoreBgImageUri;
    [ObservableProperty] private string? _globalScoreBgImageUriBo3;
    [ObservableProperty] private bool _isCampIconBlackVerEnabled;
    [ObservableProperty] private double _globalScoreTotalMargin = 390;
}
