using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Converters
{
    public class ExtensionAuthorConverter : DependencyObject, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "#作者获取失败#";
            
            try
            {
                var extensionService = App.Services.GetRequiredService<IExtensionService>();
                var extensionType = value.GetType();
                var manifest = extensionService.GetExtensionManifest(extensionType);
                return manifest?.ExtensionAuthor ?? "#作者获取失败#";
            }
            catch(Exception e)
            {
                return "#作者获取失败#" + e.Message;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}