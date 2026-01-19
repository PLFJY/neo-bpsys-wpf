using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class HomePageViewModel : ViewModelBase
{
#pragma warning disable CS8618
    public HomePageViewModel()
#pragma warning restore CS8618
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }
    public HomePageViewModel(IUpdaterService updaterService, ISettingsHostService settingsHostService)
    {
        updaterService.NewVersionInfoChanged += (sender, args) =>
        {
            ReleaseInfo = updaterService.NewVersionInfo;
        };
        ReleaseInfo = updaterService.NewVersionInfo;
        IsExpanded = settingsHostService.Settings.ShowAfterUpdateTip;
    }

    public bool IsExpanded { get; set; }

    [ObservableProperty]
    private ReleaseInfo _releaseInfo;
}
