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
    public partial class PickPageViewModel(ISharedDataService sharedDataService) : ObservableObject
    {
        public List<string> SurNameList { get; } = sharedDataService.SurNameList;

        public List<string> HunNameList { get; } = sharedDataService.HunNameList;

        [RelayCommand]
        private void Test(string index)
        {
            Debug.WriteLine(index);
        }
    }
}
