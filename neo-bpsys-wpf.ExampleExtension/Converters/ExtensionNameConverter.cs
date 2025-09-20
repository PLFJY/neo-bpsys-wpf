using System.Globalization;
using System.Windows;
using System.Windows.Data;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.ExampleExtension.Converters
{
    public class ExtensionNameConverter : DependencyObject, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "#名称获取失败#";
            
            try
            {
                var extensionType = value.GetType();
                var manifest = ExampleExtension.Instance.ExtensionService.GetExtensionManifest(extensionType);
                return manifest?.ExtensionName ?? "#名称获取失败#";
            }
            catch
            {
                return "#名称获取失败#";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}