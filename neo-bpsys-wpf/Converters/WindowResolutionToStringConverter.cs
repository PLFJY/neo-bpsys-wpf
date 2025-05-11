using System.Globalization;
using System.Windows.Data;
using static neo_bpsys_wpf.Services.FrontService;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 将WindowResolution对象列表转换为显示字符串格式的转换器
    /// 用于WPF界面绑定显示窗口分辨率选项
    /// </summary>
    public class WindowResolutionToStringConverter : IValueConverter
    {
        /// <summary>
        /// 将WindowResolution集合转换为包含显示文本的字符串集合
        /// </summary>
        /// <param name="value">需要转换的数据源（WindowResolution列表）</param>
        /// <param name="targetType">目标类型（通常为IEnumerable<string>）</param>
        /// <param name="parameter">转换参数（未使用）</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>包含格式化字符串的IEnumerable集合，当输入无效时返回null</returns>
        /// <remarks>
        /// 特殊处理1440x810分辨率添加"(默认)"标识
        /// </remarks>
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 验证输入数据类型有效性
            if (value is not List<WindowResolution> windowResolutionsList)
                return null;

            // 生成格式化字符串列表
            // 对特殊分辨率添加默认标识
            var windowResolutionStringList = windowResolutionsList.Select(resolution =>
                resolution.Width == 1440 && resolution.Height == 810
                    ? $"{resolution.Width}x{resolution.Height}(默认)"
                    : $"{resolution.Width}x{resolution.Height}"
            );

            return windowResolutionStringList;
        }

        /// <summary>
        /// 反向转换方法（不支持）
        /// </summary>
        /// <param name="value">目标值（未使用）</param>
        /// <param name="targetType">目标类型（未使用）</param>
        /// <param name="parameter">转换参数（未使用）</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>无返回值（始终抛出异常）</returns>
        /// <exception cref="NotImplementedException">始终抛出此异常表示不支持反向转换</exception>
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