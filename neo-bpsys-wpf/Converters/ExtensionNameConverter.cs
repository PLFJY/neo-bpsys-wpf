using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Converters
{
    public class ExtensionNameConverter : DependencyObject, IValueConverter
    {
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "#名称获取失败#";
            
            try
            {
                var extensionService = App.Services.GetRequiredService<IExtensionService>();
                var extensionType = value.GetType();
                var manifest = extensionService.GetExtensionManifest(extensionType);
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