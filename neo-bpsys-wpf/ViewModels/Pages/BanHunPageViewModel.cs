using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Diagnostics;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class BanHunPageViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public BanHunPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        public BanHunPageViewModel(ISharedDataService sharedDataService)
        {
            MainWindowViewModel.Swapped += MainWindowViewModel_Swapped;
            SharedDataService = sharedDataService;
        }

        private void MainWindowViewModel_Swapped(object? sender, EventArgs e)
        {
            GlobalBannedArray = SharedDataService.CurrentGame.HunTeam.GlobalBannedHunRecordArray;
            OnPropertyChanged();
        }

        [ObservableProperty]
        private Character?[] _currentBannedArray = new Character[2];

        [ObservableProperty]
        private Character?[] _globalBannedArray = new Character[3];

        [RelayCommand]
        private void ConfirmCurrentBan(object parameter)
        {
            if (parameter is not int index) return;

            SharedDataService.CurrentGame.CurrentHunBannedList[index] = CurrentBannedArray[index];
            OnPropertyChanged();
        }

        [RelayCommand]
        private void ConfirmGlobalBan(object parameter)
        {
            if (parameter is not int index) return;

            SharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[index] = GlobalBannedArray[index];
            OnPropertyChanged();
        }
    }
}
