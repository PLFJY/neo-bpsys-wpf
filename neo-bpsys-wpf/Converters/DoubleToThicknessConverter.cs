using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 将Double的Spacing值转换为Margin的Right值
    /// 实现IValueConverter接口，用于XAML绑定中的值转换场景
    /// </summary>
    public class DoubleToThicknessConverter : IValueConverter
    {
        /// <summary>
        /// 将double类型的间距值转换为右侧边距的Thickness对象
        /// </summary>
        /// <param name="value">待转换的double值（Spacing）</param>
        /// <param name="targetType">目标类型（必须为Thickness）</param>
        /// <param name="parameter">未使用的转换参数</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>右侧边距为输入值的Thickness对象，失败返回默认Thickness</returns>
        /// <remarks>
        /// 核心转换逻辑：
        /// 1. 检查输入值是否为有效double类型
        /// 2. 创建左侧/顶部/底部为0，右侧为输入值的Thickness对象
        /// </remarks>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double spacing)
            {
                return new Thickness(0, 0, spacing, 0);
            }
            return new Thickness();
        }

        /// <summary>
        /// 反向转换操作（未实现）
        /// </summary>
        /// <param name="value">目标Thickness值</param>
        /// <param name="targetType">目标数据类型</param>
        /// <param name="parameter">未使用的转换参数</param>
        /// <param name="culture">制作人也不知道功能的参数</param>
        /// <returns>始终抛出异常</returns>
        /// <remarks>
        /// 该转换器仅支持单向转换（Double -> Thickness），
        /// 不支持Thickness到Double的反向转换
        /// </remarks>
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