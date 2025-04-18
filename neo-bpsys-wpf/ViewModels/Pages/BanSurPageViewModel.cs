using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public class BanSurPageViewModel : ObservableObject
    {
        public BanSurPageViewModel()
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }
        public ISharedDataService SharedDataService { get; }
        public BanSurPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
        }
    }
}
