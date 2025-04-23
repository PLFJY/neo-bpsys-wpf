using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class PickPageViewModel : ObservableObject
    {
        public PickPageViewModel()
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        public PickPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
        }
    }
}
