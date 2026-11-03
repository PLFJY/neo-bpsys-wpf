using System.Windows;
using System.Windows.Markup;
using Wpf.Ui.Appearance;

namespace neo_bpsys_wpf.Themes;

/// <summary>
/// 按应用主题切换图标资源字典。
/// </summary>
/// <remarks>
/// 主题切换不应重新解析 XAML 并重新解码所有图标。这里把深色/浅色图标字典各加载一次，
/// 后续只替换 <see cref="ResourceDictionary.MergedDictionaries"/> 中的引用，缩短主题切换的 UI 停顿。
/// </remarks>
[Localizability(LocalizationCategory.Ignore)]
[Ambient]
[UsableDuringInitialization(true)]
public class IconThemesDictionary : ResourceDictionary
{
    private const string IconsDictionaryPath =
        "pack://application:,,,/neo-bpsys-wpf;component/Themes/";

    private static readonly Lazy<ResourceDictionary> DarkIcons = new(
        () => LoadIconsDictionary("Dark"));

    private static readonly Lazy<ResourceDictionary> LightIcons = new(
        () => LoadIconsDictionary("Light"));

    /// <summary>
    /// 设置默认应用主题。
    /// </summary>
    public ApplicationTheme Theme
    {
        set => SetSourceBasedOnSelectedTheme(value);
    }

    /// <summary>
    /// 初始化图标主题字典。
    /// </summary>
    public IconThemesDictionary()
    {
        SetSourceBasedOnSelectedTheme(ApplicationTheme.Light);
    }

    private void SetSourceBasedOnSelectedTheme(ApplicationTheme? selectedApplicationTheme)
    {
        var dictionary = selectedApplicationTheme == ApplicationTheme.Light
            ? LightIcons.Value
            : DarkIcons.Value;

        MergedDictionaries.Clear();
        MergedDictionaries.Add(dictionary);
    }

    private static ResourceDictionary LoadIconsDictionary(string themeName) =>
        new()
        {
            Source = new Uri($"{IconsDictionaryPath}Icons.{themeName}.xaml", UriKind.Absolute)
        };
}
