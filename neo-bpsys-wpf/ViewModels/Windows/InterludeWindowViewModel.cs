using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    /// <summary>
    /// 过场画面窗口的视图模型，用于管理天赋技能图片显示和设计模式状态
    /// </summary>
    public partial class InterludeWindowViewModel : ObservableObject
    {
#pragma warning disable CS8618
        /// <summary>
        /// 设计时使用的构造函数，配合IsDesignTimeCreatable=True特性
        /// </summary>
        public InterludeWindowViewModel()
#pragma warning restore CS8618
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        /// <summary>
        /// 获取共享数据服务实例
        /// </summary>
        public ISharedDataService SharedDataService { get; }

        /// <summary>
        /// 指示当前是否处于设计模式的观察属性
        /// </summary>
        [ObservableProperty]
        private bool _isDesignMode = false;

        /// <summary>
        /// 使用依赖注入初始化视图模型
        /// </summary>
        /// <param name="sharedDataService">共享数据服务接口，用于跨组件数据传递</param>
        public InterludeWindowViewModel(ISharedDataService sharedDataService)
        {
            // 订阅设计模式变更事件
            FrontManagePageViewModel.DesignModeChanged += OnDesignModeChanged;

            // 初始化共享数据服务
            SharedDataService = sharedDataService;

            // 初始化求生者阵营天赋图片
            BorrowedTimeImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Sur, "回光返照");
            FlywheelEffectImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Sur, "飞轮效应");
            KneeJerkReflexImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Sur, "膝跳反射");
            TideTurnerImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Sur, "化险为夷");

            // 初始化监管者阵营天赋图片
            ConfinedSpaceImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Hun, "禁闭空间");
            DetentionImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Hun, "挽留");
            InsolenceImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Hun, "张狂");
            TrumpCardImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Hun, "底牌");
        }

        /// <summary>
        /// 处理设计模式变更事件
        /// </summary>
        private void OnDesignModeChanged(object? sender, DesignModeChangedEventArgs e)
        {
            IsDesignMode = e.IsDesignMode;
        }

        // region 求生者阵营天赋图片
        /// <summary>
        /// 回光返照天赋图片源（求生者阵营）
        /// </summary>
        [ObservableProperty]
        private ImageSource? _borrowedTimeImageSource;

        /// <summary>
        /// 飞轮效应天赋图片源（求生者阵营）
        /// </summary>
        [ObservableProperty]
        private ImageSource? _flywheelEffectImageSource;

        /// <summary>
        /// 膝跳反射天赋图片源（求生者阵营）
        /// </summary>
        [ObservableProperty]
        private ImageSource? _kneeJerkReflexImageSource;

        /// <summary>
        /// 化险为夷天赋图片源（求生者阵营）
        /// </summary>
        [ObservableProperty]
        private ImageSource? _tideTurnerImageSource;

        // region 监管者阵营天赋图片
        /// <summary>
        /// 禁闭空间天赋图片源（监管者阵营）
        /// </summary>
        [ObservableProperty]
        private ImageSource? _confinedSpaceImageSource;

        /// <summary>
        /// 挽留天赋图片源（监管者阵营）
        /// </summary>
        [ObservableProperty]
        private ImageSource? _detentionImageSource;

        /// <summary>
        /// 张狂天赋图片源（监管者阵营）
        /// </summary>
        [ObservableProperty]
        private ImageSource? _insolenceImageSource;

        /// <summary>
        /// 底牌天赋图片源（监管者阵营）
        /// </summary>
        [ObservableProperty]
        private ImageSource? _trumpCardImageSource;
    }
}