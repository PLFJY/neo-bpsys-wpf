using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 将ApplicationTheme枚举值转换为布尔值的转换器
    /// true表示深色主题，false表示浅色主题
    /// </summary>
    public class ApplicationThemeToBooleanConverter : IValueConverter
    {
        /// <summary>
        /// 将ApplicationTheme转换为布尔值
        /// </summary>
        /// <param name="value">源值，应为ApplicationTheme类型</param>
        /// <param name="targetType">目标绑定属性的类型（未使用）</param>
        /// <param name="parameter">转换参数（未使用）</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>当value为ApplicationTheme.Dark时返回true，否则返回false</returns>
        /// <exception cref="ArgumentException">当输入值不是ApplicationTheme类型时抛出</exception>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ApplicationTheme applicationTheme)
                throw new ArgumentException();
            return applicationTheme == ApplicationTheme.Dark;
        }

        /// <summary>
        /// 将布尔值转换回ApplicationTheme枚举值
        /// </summary>
        /// <param name="value">要转换的布尔值</param>
        /// <param name="targetType">目标绑定属性的类型（未使用）</param>
        /// <param name="parameter">转换参数（未使用）</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>当value为true时返回ApplicationTheme.Dark，否则返回ApplicationTheme.Light</returns>
        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            return (bool)value ? ApplicationTheme.Dark : ApplicationTheme.Light;
        }
    }
}