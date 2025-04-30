using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.Diagnostics;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class BanHunPageViewModel : ObservableObject
    {
        public BanHunPageViewModel()
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        public BanHunPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
            CurrentBannedArray = new Character[2];
            GlobalBannedArray = new Character[3];
        }

        [ObservableProperty]
        private Character[] _currentBannedArray;

        [ObservableProperty]
        private Character[] _globalBannedArray;

        [RelayCommand]
        private void ConfirmCurrentBan(object parameter)
        {
            if (parameter is not int index) return;

            SharedDataService.CurrentGame.HunTeam.CurrentBannedHunArray[index] = CurrentBannedArray[index];
        }

        [RelayCommand]
        private void ConfirmGlobalBan(object parameter)
        {
            if (parameter is not int index) return;

            SharedDataService.CurrentGame.HunTeam.GlobalBannedHunArray[index] = GlobalBannedArray[index];
        }
    }
}
