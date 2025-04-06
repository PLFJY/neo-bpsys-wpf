using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class BanHunPageViewModel : ObservableObject
    {
        private readonly ISharedDataService _sharedDataService;
        public List<string> HunNameList { get; }

        public BanHunPageViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
            HunNameList = _sharedDataService.HunNameList;
        }
    }
}
