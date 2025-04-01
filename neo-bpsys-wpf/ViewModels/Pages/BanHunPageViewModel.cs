using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class BanHunPageViewModel(ISharedDataService sharedDataService) : ObservableObject
    {
        public List<string> HunNameList { get; } = sharedDataService.HunNameList;
    }
}
