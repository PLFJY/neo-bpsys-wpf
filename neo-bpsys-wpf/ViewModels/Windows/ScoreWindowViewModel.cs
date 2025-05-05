using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class ScoreWindowViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public ScoreWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        public ScoreWindowViewModel(ISharedDataService sharedDataService)
        {
            FrontManagePageViewModel.DesignModeChanged += OnDesignModeChanged;
            SharedDataService = sharedDataService;
        }

        [ObservableProperty]
        private bool _isDesignMode = false;

        private void OnDesignModeChanged(object? sender, DesignModeChangedEventArgs e)
        {
            IsDesignMode = e.IsDesignMode;
        }
    }
}
