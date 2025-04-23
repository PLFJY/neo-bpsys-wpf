using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class MapBpPageViewModel : ObservableObject
    {
        public ISharedDataService SharedDataService { get; }

        public MapBpPageViewModel()
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public MapBpPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
        }

        [RelayCommand]
        private void MapPick(string tagName)
        {
            if (Enum.TryParse(tagName, out Map map))
            {
                SharedDataService.CurrentGame.PickedMap = map;
            }
            else
            {
                SharedDataService.CurrentGame.PickedMap = null;
            }
        }

        [RelayCommand]
        private void MapBan(string tagName)
        {
            if (Enum.TryParse(tagName, out Map map))
            {
                SharedDataService.CurrentGame.BandedMap = map;
            }
            else
            {
                SharedDataService.CurrentGame.BandedMap = null;
            }
        }
    }
}
