using System.Windows;
using System.Windows.Markup;
using Wpf.Ui.Appearance;

namespace neo_bpsys_wpf.Theme
{
    /// <summary>
    /// 图标主题资源字典，根据应用主题动态切换图标资源
    /// </summary>
    [Localizability(LocalizationCategory.Ignore)]
    [Ambient]
    [UsableDuringInitialization(true)]
    public class IconThemesDictionary : ResourceDictionary
    {
        // 图标资源字典基础路径
        private const string IconsDictionaryPath =
            "pack://application:,,,/neo-bpsys-wpf;component/Themes/";

        /// <summary>
        /// 设置应用主题并更新图标资源
        /// </summary>
        /// <value>指定要应用的主题类型（浅色/深色）</value>
        public ApplicationTheme Theme
        {
            set => SetSourceBasedOnSelectedTheme(value);
        }

        /// <summary>
        /// 构造函数初始化默认使用浅色主题
        /// </summary>
        public IconThemesDictionary()
        {
            SetSourceBasedOnSelectedTheme(ApplicationTheme.Light);
        }

        /// <summary>
        /// 根据选定主题设置资源字典路径
        /// </summary>
        /// <param name="selectedApplicationTheme">
        /// 选定的应用主题，null值时默认使用深色主题
        /// </param>
        private void SetSourceBasedOnSelectedTheme(ApplicationTheme? selectedApplicationTheme)
        {
            // 根据主题选择对应的字典名称
            var themeName = selectedApplicationTheme switch
            {
                ApplicationTheme.Dark => "Dark",
                ApplicationTheme.Light => "Light",
                _ => "Dark",
            };

            // 组合完整的资源字典路径并更新资源
            Source = new Uri($"{IconsDictionaryPath}Icons.{themeName}.xaml", UriKind.Absolute);
        }
    }
}