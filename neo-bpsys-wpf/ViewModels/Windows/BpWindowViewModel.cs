using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    /// <summary>
    /// bp窗口的视图模型类，继承自ObservableObject实现属性变更通知
    /// </summary>
    public partial class BpWindowViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        /// <summary>
        /// 设计模式专用构造函数，用于XAML设计器预览
        /// </summary>
        public BpWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        /// <summary>
        /// 标识当前是否处于设计模式（用于界面设计器）
        /// </summary>
        [ObservableProperty]
        private bool _isDesignMode = false;

        /// <summary>
        /// 共享数据服务接口，用于跨组件数据传递
        /// </summary>
        public ISharedDataService SharedDataService { get; }

        /// <summary>
        /// 主构造函数，初始化bp窗口视图模型
        /// </summary>
        /// <param name="sharedDataService">共享数据服务实例，用于依赖注入</param>
        public BpWindowViewModel(ISharedDataService sharedDataService)
        {
            // 订阅设计模式变更事件
            FrontManagePageViewModel.DesignModeChanged += OnDesignModeChanged;

            // 初始化共享服务和图像资源
            SharedDataService = sharedDataService;
            CurrentBanLockImage = ImageHelper.GetUiImageSource("CurrentBanLock");
            GlobalBanLockImage = ImageHelper.GetUiImageSource("GlobalBanLock");
        }

        /// <summary>
        /// 处理设计模式变更事件
        /// </summary>
        /// <param name="sender">事件源对象</param>
        /// <param name="e">包含设计模式状态的事件参数</param>
        private void OnDesignModeChanged(object? sender, DesignModeChangedEventArgs e)
        {
            IsDesignMode = e.IsDesignMode;
        }

        /// <summary>
        /// 当前禁用锁定状态图标（可空类型）
        /// </summary>
        [ObservableProperty]
        private ImageSource? _currentBanLockImage;

        /// <summary>
        /// 全局禁用锁定状态图标（可空类型）
        /// </summary>
        [ObservableProperty]
        private ImageSource? _globalBanLockImage;
    }
}