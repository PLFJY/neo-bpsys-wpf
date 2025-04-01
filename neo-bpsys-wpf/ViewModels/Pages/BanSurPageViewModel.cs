using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public class BanSurPageViewModel(ISharedDataService sharedDataService) : ObservableObject
    {
        public List<string> SurNameList { get; } = sharedDataService.SurNameList;
    }
}
