using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 布尔值转换为可见性
    /// <para>true 对应 Collapsed</para>
    /// <para>false 对应 Visible</para>
    /// </summary>
    public class BooleanToReverseVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为Visibility枚举值，反转常规映射关系。
        /// </summary>
        /// <param name="value">输入的布尔值，true表示折叠，false表示可见</param>
        /// <param name="targetType">目标绑定属性的类型，通常为Visibility</param>
        /// <param name="parameter">转换参数，未在此实现中使用</param>
        /// <param name="culture">用于本地化的制作人也不知道功能的参数，未在此实现中使用</param>
        /// <returns>当value为true时返回Visibility.Collapsed，否则返回Visibility.Visible</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// 反向转换方法，当前未实现
        /// </summary>
        /// <param name="value">目标Visibility值</param>
        /// <param name="targetType">目标数据类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="culture">制作人也不知道功能的参数</param>
        /// <returns>始终抛出NotImplementedException</returns>
        /// <exception cref="NotImplementedException">该转换器仅支持单向绑定</exception>
        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException();
        }
    }
}