using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Services;
using System.Diagnostics;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    /// <summary>
    /// 地图BP页面视图模型，实现地图选择和禁用功能的数据绑定与业务逻辑
    /// 负责管理当前选中地图和禁用地图的状态同步
    /// </summary>
    public partial class MapBpPageViewModel : ObservableObject
    {
        /// <summary>
        /// 共享数据服务接口，用于跨页面数据同步
        /// </summary>
        public ISharedDataService SharedDataService { get; }

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        /// <summary>
        /// 设计时构造函数（用于XAML设计器预览）
        /// </summary>
        public MapBpPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        /// <summary>
        /// 运行时构造函数，通过依赖注入初始化共享数据服务
        /// </summary>
        /// <param name="sharedDataService">共享数据服务实例</param>
        public MapBpPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
        }

        private Map? _pickedMap;

        /// <summary>
        /// 当前选中地图属性，双向绑定到UI元素
        /// 设置值时同步更新共享数据服务中的当前游戏配置
        /// </summary>
        public Map? PickedMap
        {
            get => _pickedMap;
            set
            {
                _pickedMap = value;
                SharedDataService.CurrentGame.PickedMap = _pickedMap;
                OnPropertyChanged();
            }
        }

        private Map? _bannedMap;

        /// <summary>
        /// 当前禁用地图属性，双向绑定到UI元素
        /// 设置值时同步更新共享数据服务中的当前游戏配置
        /// </summary>
        public Map? BannedMap
        {
            get => _bannedMap;
            set
            {
                _bannedMap = value;
                SharedDataService.CurrentGame.BannedMap = _bannedMap;
                OnPropertyChanged();
            }
        }
    }
}