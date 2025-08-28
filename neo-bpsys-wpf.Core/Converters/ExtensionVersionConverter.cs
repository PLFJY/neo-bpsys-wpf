using System.Globalization;
using System.Windows.Data;
using neo_bpsys_wpf.Core.Services;

namespace neo_bpsys_wpf.Core.Converters
{
    public class ExtensionVersionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "#版本获取失败#";
            
            try
            {
                var extensionType = value.GetType();
                var manifest = ExtensionManager.Instance().GetExtensionManifest(extensionType);
                return manifest?.ExtensionVersion ?? "#版本获取失败#";
            }
            catch
            {
                return "#版本获取失败#";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}