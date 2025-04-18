using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Services;

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
        }
    }
}
