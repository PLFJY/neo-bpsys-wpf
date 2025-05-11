using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using neo_bpsys_wpf.CustomControls;
using neo_bpsys_wpf.Models;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 将CharacterChanger对象转换为CharacterChangerCommandParameter对象的值转换器
    /// 用于在WPF绑定中将索引值转换为命令参数
    /// </summary>
    public class CharacterChangerCommandParameterConverter : IValueConverter
    {
        /// <summary>
        /// 将CharacterChanger对象转换为CharacterChangerCommandParameter对象
        /// </summary>
        /// <param name="value">要转换的源数据（CharacterChanger对象）</param>
        /// <param name="targetType">目标类型（未使用）</param>
        /// <param name="parameter">转换参数（IndexToButtonContentConverter实例）</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>转换后的CharacterChangerCommandParameter对象，转换失败时返回Binding.DoNothing</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 尝试将输入值转换为CharacterChanger对象
            var characterChanger = value as CharacterChanger;
            // 尝试将参数转换为索引到按钮内容转换器
            var contentConverter = parameter as IndexToButtonContentConverter;

            // 当两个转换都成功时执行转换逻辑
            if (characterChanger != null && contentConverter != null)
            {
                int index = characterChanger.Index;
                // 使用内容转换器将索引转换为按钮内容
                int buttonContent = (int)contentConverter.Convert(index, typeof(int), parameter, culture);
                // 创建并返回新的命令参数对象（按钮内容减1作为参数）
                return new CharacterChangerCommandParameter(index, buttonContent - 1);
            }
            // 转换失败时返回DoNothing
            return Binding.DoNothing;
        }

        /// <summary>
        /// 不支持反向转换操作
        /// </summary>
        /// <param name="value">目标值（未使用）</param>
        /// <param name="targetType">目标类型（未使用）</param>
        /// <param name="parameter">转换参数（未使用）</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>无返回值，始终抛出异常</returns>
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