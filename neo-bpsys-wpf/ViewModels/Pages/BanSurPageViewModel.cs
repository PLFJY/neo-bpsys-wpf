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
        private readonly ISharedDataService _sharedDataService;
        public List<string> SurNameList { get; }

        public BanSurPageViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
            SurNameList = _sharedDataService.SurNameList;
        }
    }
}
