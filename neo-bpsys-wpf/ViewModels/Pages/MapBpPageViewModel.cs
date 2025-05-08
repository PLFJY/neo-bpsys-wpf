using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Services;
using System.Diagnostics;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class MapBpPageViewModel : ObservableObject
    {
        public ISharedDataService SharedDataService { get; }

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public MapBpPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public MapBpPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
        }

        private Map? _pickedMap;

        public Map? PickedMap
        {
            get => _pickedMap;
            set
            {
                _pickedMap = value;
                SharedDataService.CurrentGame.PickedMap = _pickedMap;
                OnPropertyChanged();
            }
        }

        private Map? _bannedMap;

        public Map? BannedMap
        {
            get => _bannedMap;
            set
            {
                _bannedMap = value;
                SharedDataService.CurrentGame.BannedMap = _bannedMap;
                OnPropertyChanged();
            }
        }
    }
}
