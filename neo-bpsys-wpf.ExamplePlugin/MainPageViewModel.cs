using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.ExamplePlugin;

public partial class MainPageViewModel : ViewModelBase
{
    [ObservableProperty] 
    private int _counter;

    [RelayCommand]
    private void Plus1()
    {
        Counter++;
    }

}
