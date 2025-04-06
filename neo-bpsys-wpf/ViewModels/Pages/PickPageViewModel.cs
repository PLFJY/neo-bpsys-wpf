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
        private readonly ISharedDataService _sharedDataService;

        public List<string> SurNameList { get; }

        public List<string> HunNameList { get; }

        public PickPageViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
            SurNameList = _sharedDataService.SurNameList;
            HunNameList = _sharedDataService.HunNameList;
        }

    }
}
