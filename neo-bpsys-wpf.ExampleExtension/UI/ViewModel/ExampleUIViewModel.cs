using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Extensions;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.ExampleExtension.UI.ViewModel;

public class ExampleUIViewModel : ViewModelBase
{
    public ExampleUIViewModel()
    {
        AddMainTeamMinorPointsCommand = new RelayCommand(AddMainTeamMinorPoints);
        DisableSelfCommand = new RelayCommand(DisableSelf);
        ExtensionManager.Instance().SharedDataService.MainTeam.Score.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName.Equals(nameof(Score.MinorPoints)))
            {
                OnPropertyChanged(nameof(MainTeamMinorPoints));
            }
        };
    }
    
    public ExampleExtension Extension => ExampleExtension.Instance;
    
    public int MainTeamMinorPoints => ExtensionManager.Instance().SharedDataService.MainTeam.Score.MinorPoints;

    public ICommand AddMainTeamMinorPointsCommand { get; private set; }
    private void AddMainTeamMinorPoints()
    {
        ExtensionManager.Instance().SharedDataService.MainTeam.Score.MinorPoints++;
    }
    
    public ICommand DisableSelfCommand { get; private set; }
    private void DisableSelf()
    {
        ExtensionManager.Instance().DisableExtension(ExampleExtension.Instance);
    }
}