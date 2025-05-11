using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
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
    /// 将Score对象转换为前端显示的字符串格式的转换器
    /// 转换格式示例："W:3 D:1"
    /// </summary>
    public class ScoreToStringConverterInFront : IValueConverter
    {
        /// <summary>
        /// 将Score值类型转换为字符串表示形式
        /// </summary>
        /// <param name="value">待转换的值，应为Score类型</param>
        /// <param name="targetType">目标类型（未使用）</param>
        /// <param name="parameter">转换参数（未使用）</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>格式为"W:{Win} D:{Tie}"的字符串，非Score类型返回Binding.DoNothing</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 检查输入值是否为Score类型
            if (value is not Score score) return Binding.DoNothing;

            // 返回格式化字符串，显示胜负和平局次数
            return $"W:{score.Win} D:{score.Tie}";
        }

        /// <summary>
        /// 反向转换方法（未实现）
        /// </summary>
        /// <param name="value">待转换的值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="culture">制作人也不知道功能的参数</param>
        /// <returns>未实现，调用时抛出异常</returns>
        /// <exception cref="NotImplementedException">始终抛出未实现异常</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}