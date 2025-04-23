using System.Windows;
using System.Windows.Markup;
using Wpf.Ui.Appearance;

namespace neo_bpsys_wpf.Theme
{
    [Localizability(LocalizationCategory.Ignore)]
    [Ambient]
    [UsableDuringInitialization(true)]
    public class IconThemesDictionary : ResourceDictionary
    {
        private const string IconsDictionaryPath =
            "pack://application:,,,/neo-bpsys-wpf;component/Themes/";

        /// <summary>
        /// Sets the default application theme.
        /// </summary>
        public ApplicationTheme Theme
        {
            set => SetSourceBasedOnSelectedTheme(value);
        }

        public IconThemesDictionary()
        {
            SetSourceBasedOnSelectedTheme(ApplicationTheme.Light);
        }

        private void SetSourceBasedOnSelectedTheme(ApplicationTheme? selectedApplicationTheme)
        {
            var themeName = selectedApplicationTheme switch
            {
                ApplicationTheme.Dark => "Dark",
                ApplicationTheme.Light => "Light",
                _ => "Dark",
            };

            Source = new Uri($"{IconsDictionaryPath}Icons.{themeName}.xaml", UriKind.Absolute);
        }
    }
}
