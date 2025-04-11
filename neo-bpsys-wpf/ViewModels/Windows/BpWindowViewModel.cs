using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class BpWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isDesignMode = false;
        public BpWindowViewModel()
        {
            FrontManagePageViewModel.DesignModeChanged += OnDesignModeChanged;
        }

        private void OnDesignModeChanged(object? sender, DesignModeChangedEventArgs e)
        {
            IsDesignMode = e.IsDesignMode;
        }
    }
}
