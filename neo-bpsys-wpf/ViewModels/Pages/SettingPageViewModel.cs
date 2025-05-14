using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    /// <summary>
    /// 设置页面视图模型，负责管理应用程序设置界面的数据绑定与业务逻辑。
    /// 实现了MVVM模式，通过ObservableObject基类支持属性变更通知。
    /// </summary>
    public partial class SettingPageViewModel : ObservableObject
    {
        /// <summary>
        /// 初始化SettingPageViewModel的新实例。
        /// 该构造函数专为设计时实例化设计（配合IsDesignTimeCreatable=True使用），
        /// 运行时通常由依赖注入容器管理实例创建。
        /// </summary>
        /// <remarks>
        /// 初始化过程中会加载当前应用程序的程序集版本信息，
        /// 并将其格式化为"版本 v{主版本.次版本.构建号}"的显示格式。
        /// </remarks>
        public SettingPageViewModel()
        {
            // Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
            AppVersion = "版本 v" + App.ResourceAssembly.GetName().Version!.ToString();
        }

        /// <summary>
        /// 获取或设置应用程序版本显示字符串。
        /// 该属性通过[ObservableProperty]特性自动生成属性变更通知逻辑，
        /// 保持与XAML界面绑定的版本信息控件同步更新。
        /// </summary>
        /// <value>
        /// 格式为"版本 v{主版本.次版本.构建号}"的字符串值，
        /// 例如："版本 v1.2.3.4"
        /// </value>
        [ObservableProperty]
        private string _appVersion = string.Empty;
    }
}