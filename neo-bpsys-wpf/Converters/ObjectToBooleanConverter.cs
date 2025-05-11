using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 将对象与参数进行比较并转换为布尔值的转换器
    /// </summary>
    public class ObjectToBooleanConverter : IValueConverter
    {
        /// <summary>
        /// 将源对象转换为目标布尔值
        /// </summary>
        /// <param name="value">要转换的源对象值</param>
        /// <param name="targetType">目标类型（未使用）</param>
        /// <param name="parameter">用于比较的参数对象</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>布尔值表示对象是否等于参数</returns>
        /// <remarks>
        /// 使用对象重写的Equals方法进行比较，适用于需要自定义比较逻辑的场景
        /// </remarks>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.Equals(parameter); // 利用模型重写的 Equals 方法
        }

        /// <summary>
        /// 将布尔值转换回源对象
        /// </summary>
        /// <param name="value">要转换的布尔值</param>
        /// <param name="targetType">目标绑定类型</param>
        /// <param name="parameter">作为比较基准的对象参数</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>当值为true时返回参数对象，否则返回Binding.DoNothing</returns>
        /// <remarks>
        /// 用于双向绑定时的反向转换，仅当布尔值为true时触发源更新
        /// </remarks>
        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            return (bool)value ? parameter : Binding.DoNothing;
        }
    }
}