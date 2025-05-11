using System.Globalization;
using System.Windows.Data;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 将Camp枚举值转换为对应的中文字符串表示的转换器。
    /// 实现IValueConverter接口，用于WPF绑定中的值转换。
    /// </summary>
    public class CampToStringConverter : IValueConverter
    {
        /// <summary>
        /// 将Camp枚举值转换为带有前缀的中文字符串。
        /// </summary>
        /// <param name="value">要转换的源值，应为Camp枚举类型。</param>
        /// <param name="targetType">目标绑定属性的类型（未使用）。</param>
        /// <param name="parameter">转换参数（未使用）。</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）。</param>
        /// <returns>转换后的字符串，如"当前状态：求生者"；若输入无效则返回空字符串。</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 如果输入值不是Camp类型，返回空字符串以避免转换错误
            if (value is not Camp camp)
                return string.Empty;

            // 根据Camp枚举值确定对应的中文描述
            var campWord = camp == Camp.Sur ? "求生者" : "监管者";

            return $"当前状态：{campWord}";
        }

        /// <summary>
        /// 此转换器仅支持单向转换，反向转换未实现。
        /// </summary>
        /// <exception cref="NotImplementedException">始终抛出此异常。</exception>
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