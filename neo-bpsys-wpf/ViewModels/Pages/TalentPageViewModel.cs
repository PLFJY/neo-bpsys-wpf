using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    /// <summary>
    /// 天赋页面视图模型，管理天赋页面的数据和交互逻辑
    /// </summary>
    public partial class TalentPageViewModel : ObservableObject
    {
        /// <summary>
        /// 设计时使用的构造函数（运行时不会被调用）
        /// </summary>
        /// <remarks>
        /// 配合IsDesignTimeCreatable=True特性用于XAML设计器预览
        /// </remarks>
#pragma warning disable CS8618
        public TalentPageViewModel()
#pragma warning restore CS8618
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        /// <summary>
        /// 获取共享数据服务实例
        /// </summary>
        public ISharedDataService SharedDataService { get; }

        /// <summary>
        /// 主构造函数，接收共享数据服务依赖注入
        /// </summary>
        /// <param name="sharedDataService">共享数据服务接口，用于跨组件数据交互</param>
        public TalentPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
        }

        private Enums.Trait? _selectedTrait = null;

        /// <summary>
        /// 当前选中的特质属性
        /// </summary>
        /// <remarks>
        /// 当设置值时：
        /// 1. 更新共享数据中玩家的特质属性
        /// 2. 触发属性变更通知更新UI绑定
        /// </remarks>
        public Enums.Trait? SelectedTrait
        {
            get { return _selectedTrait; }
            set
            {
                _selectedTrait = value;
                // 将选择的特质同步到共享数据模型的玩家对象
                SharedDataService.CurrentGame.HunPlayer.Trait = new(_selectedTrait);
                OnPropertyChanged();
            }
        }
    }
}