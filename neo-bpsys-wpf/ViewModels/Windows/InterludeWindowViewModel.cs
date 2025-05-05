using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class InterludeWindowViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public InterludeWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        [ObservableProperty]
        private bool _isDesignMode = false;

        public InterludeWindowViewModel(ISharedDataService sharedDataService)
        {
            FrontManagePageViewModel.DesignModeChanged += OnDesignModeChanged;
            SharedDataService = sharedDataService;
            //Sur
            BorrowedTimeImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Sur, "回光返照");
            FlywheelEffectImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Sur, "飞轮效应");
            KneeJerkReflexImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Sur, "膝跳反射");
            TideTurnerImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Sur, "化险为夷");
            //Hun
            ConfinedSpaceImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Hun, "禁闭空间");
            DetentionImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Hun, "挽留");
            InsolenceImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Hun, "张狂");
            TrumpCardImageSource = ImageHelper.GetTalentImageSource(Enums.Camp.Hun, "底牌");
        }

        private void OnDesignModeChanged(object? sender, DesignModeChangedEventArgs e)
        {
            IsDesignMode = e.IsDesignMode;
        }

        //talent imageSource
        //Sur
        [ObservableProperty]
        private ImageSource? _borrowedTimeImageSource;
        [ObservableProperty]
        private ImageSource? _flywheelEffectImageSource;
        [ObservableProperty]
        private ImageSource? _kneeJerkReflexImageSource;
        [ObservableProperty]
        private ImageSource? _tideTurnerImageSource;
        //Hun
        [ObservableProperty]
        private ImageSource? _confinedSpaceImageSource;
        [ObservableProperty]
        private ImageSource? _detentionImageSource;
        [ObservableProperty]
        private ImageSource? _insolenceImageSource;
        [ObservableProperty]
        private ImageSource? _trumpCardImageSource;
    }
}
