using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class ScoreManualWindowViewModel : ViewModelBase
{
    private readonly ISharedDataService _sharedDataService;

    public ScoreManualWindowViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    public ScoreManualWindowViewModel(ISharedDataService sharedDataService)
    {
        _sharedDataService = sharedDataService;
    }

    [RelayCommand]
    private void EditMainWin(int diff)
    {
        _sharedDataService.MainTeam.Score.Win += diff;
    }

    [RelayCommand]
    private void EditMainTie(int diff)
    {
        _sharedDataService.MainTeam.Score.Tie += diff;
    }

    [RelayCommand]
    private void EditMainMinorPoints(int diff)
    {
        _sharedDataService.MainTeam.Score.MinorPoints += diff;
    }

    [RelayCommand]
    private void EditAwayWin(int diff)
    {
        _sharedDataService.AwayTeam.Score.Win += diff;
    }

    [RelayCommand]
    private void EditAwayTie(int diff)
    {
        _sharedDataService.AwayTeam.Score.Tie += diff;
    }

    [RelayCommand]
    private void EditAwayMinorPoints(int diff)
    {
        _sharedDataService.AwayTeam.Score.MinorPoints += diff;
    }

    [RelayCommand]
    private void ClearMinorPoints()
    {
        _sharedDataService.MainTeam.Score.MinorPoints = 0;
        _sharedDataService.AwayTeam.Score.MinorPoints = 0;
    }
}