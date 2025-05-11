using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 将Index转换为CharacterChanger的Button Content，对应Button的Nmae的数字-1
    /// </summary>
    public class IndexToButtonContentConverter : IValueConverter
    {
        public int ButtonIndex { get; set; }

        /// <summary>
        /// 将控件索引转换为对应的按钮内容值
        /// </summary>
        /// <param name="value">绑定源的值（预期为int类型，表示控件索引）</param>
        /// <param name="targetType">绑定目标的类型</param>
        /// <param name="parameter">附加的转换参数（未使用）</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>转换后的按钮内容值，若无法转换则返回Binding.DoNothing</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int controlIndex = (int)value;
            var buttonName = GetButtonName(controlIndex);
            if (buttonName == null)
                return Binding.DoNothing;
            return buttonName;
        }

        /// <summary>
        /// 根据控件索引和按钮索引确定对应的按钮编号
        /// </summary>
        /// <param name="controlIndex">控件索引（0-3）</param>
        /// <returns>对应的按钮编号（1-4）或null</returns>
        private int? GetButtonName(int controlIndex)
        {
            // 处理不同控件索引的映射逻辑
            switch (controlIndex)
            {
                // 处理第一个控件索引（0）的映射规则
                case 0:
                    if (ButtonIndex == 1)
                        return 2;
                    else if (ButtonIndex == 2)
                        return 3;
                    else if (ButtonIndex == 3)
                        return 4;
                    else
                        return null;

                // 处理第二个控件索引（1）的映射规则
                case 1:
                    if (ButtonIndex == 1)
                        return 1;
                    else if (ButtonIndex == 2)
                        return 3;
                    else if (ButtonIndex == 3)
                        return 4;
                    else
                        return null;

                // 处理第三个控件索引（2）的映射规则
                case 2:
                    if (ButtonIndex == 1)
                        return 1;
                    else if (ButtonIndex == 2)
                        return 2;
                    else if (ButtonIndex == 3)
                        return 4;
                    else
                        return null;

                // 处理第四个控件索引（3）的映射规则
                case 3:
                    if (ButtonIndex == 1)
                        return 1;
                    else if (ButtonIndex == 2)
                        return 2;
                    else if (ButtonIndex == 3)
                        return 3;
                    else
                        return null;

                // 不支持的控件索引直接返回null
                default:
                    return null;
            }
        }

        /// <summary>
        /// 不支持反向转换操作
        /// </summary>
        /// <param name="value">目标值（未使用）</param>
        /// <param name="targetType">目标类型（未使用）</param>
        /// <param name="parameter">附加参数（未使用）</param>
        /// <param name="culture">制作人也不知道功能的参数（未使用）</param>
        /// <returns>无返回值，始终抛出异常</returns>
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