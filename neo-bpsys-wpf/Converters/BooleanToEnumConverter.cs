using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 将布尔值转换为枚举值的比较结果，实现双向绑定转换
    /// 用于在WPF界面中将布尔状态映射到特定枚举值的比较操作
    /// </summary>
    public class BooleanToEnumConverter : IValueConverter
    {
        /// <summary>
        /// 将源值转换为目标类型
        /// </summary>
        /// <param name="value">要转换的源值（通常为布尔类型）</param>
        /// <param name="targetType">目标类型（未实际使用）</param>
        /// <param name="parameter">比较参数（与源值进行相等比较的枚举值）</param>
        /// <param name="culture">制作人也不知道功能的参数（未实际使用）</param>
        /// <returns>
        /// 当value不为null时返回value.Equals(parameter)的结果
        /// 当value为null时返回Binding.DoNothing以保持绑定有效性
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 处理空值情况以避免转换异常
            if (value == null)
                return Binding.DoNothing;

            // 执行布尔值与参数的相等比较
            return value.Equals(parameter);
        }

        /// <summary>
        /// 将目标值转换回源类型（反向转换）
        /// </summary>
        /// <param name="value">要转换的布尔值</param>
        /// <param name="targetType">目标类型（未实际使用）</param>
        /// <param name="parameter">返回参数（当value为true时返回该值）</param>
        /// <param name="culture">制作人也不知道功能的参数（未实际使用）</param>
        /// <returns>
        /// 当value为true时返回parameter
        /// 否则返回null以保持绑定有效性
        /// </returns>
        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 仅当布尔值为true时返回参数对象
            if (value is bool b && b)
                return parameter;

            // 其他情况返回null
            return null;
        }
    }
}