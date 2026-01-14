using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class ScoreManualWindowViewModel : ViewModelBase
{
    private readonly ISharedDataService _sharedDataService;

#pragma warning disable CS8618 
    public ScoreManualWindowViewModel()
#pragma warning restore CS8618 
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
    private void EditMainGameScores(int diff)
    {
        _sharedDataService.MainTeam.Score.GameScores += diff;
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
    private void EditAwayGameScores(int diff)
    {
        _sharedDataService.AwayTeam.Score.GameScores += diff;
    }

    [RelayCommand]
    private void ClearGameScores()
    {
        _sharedDataService.MainTeam.Score.GameScores = 0;
        _sharedDataService.AwayTeam.Score.GameScores = 0;
    }
}