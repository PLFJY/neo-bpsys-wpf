using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
