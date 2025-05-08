using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class WidgetsWindowViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public WidgetsWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        [ObservableProperty]
        private bool _isDesignMode = false;

        public ISharedDataService SharedDataService { get; }

        public WidgetsWindowViewModel(ISharedDataService sharedDataService)
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
