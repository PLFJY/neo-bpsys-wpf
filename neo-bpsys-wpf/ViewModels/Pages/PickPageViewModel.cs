using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.CustomControls;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class PickPageViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public PickPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        public PickPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
            PickedSurList = new Character[4];
        }

        [ObservableProperty]
        private Character?[] _pickedSurList;

        [ObservableProperty]
        private Character? _pickedHun;

        [RelayCommand]
        private void ConfirmPickedSur(object parameter)
        {
            if (parameter is not int index) return;

            SharedDataService.CurrentGame.SurPlayerList[index].Character = PickedSurList[index];
            OnPropertyChanged();
        }

        [RelayCommand]
        private void ConfirmPickedHun()
        {
            SharedDataService.CurrentGame.HunPlayer.Character = PickedHun;
            OnPropertyChanged();
        }

        [RelayCommand]
        private void SwapCharacterInPlayers(CharacterChangerCommandParameter parameter)
        {
            (SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Character,
                SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Character) =
                (SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Character,
                SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Character);

            OnPropertyChanged();
        }
    }
}
