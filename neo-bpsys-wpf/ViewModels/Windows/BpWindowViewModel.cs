using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class BpWindowViewModel : ObservableObject
    {
        public BpWindowViewModel()
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        [ObservableProperty]
        private bool _isDesignMode = false;
        public ISharedDataService SharedDataService { get; }

        public BpWindowViewModel(ISharedDataService sharedDataService)
        {
            FrontManagePageViewModel.DesignModeChanged += OnDesignModeChanged;
            SharedDataService = sharedDataService;
        }

        private void OnDesignModeChanged(object? sender, DesignModeChangedEventArgs e)
        {
            IsDesignMode = e.IsDesignMode;
        }
    }
}
