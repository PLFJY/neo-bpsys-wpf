using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 将Index转换为CharacterChanger的Button Content<br/>
/// 如果Index是0那么对应的就是1号位角色，那么剩下三个按钮则是2,3,4<br/>
/// 如果Index是1那么对应的就是2号位角色，那么剩下三个按钮则是1,3,4<br/>
/// 如果Index是2那么对应的就是3号位角色，那么剩下三个按钮则是1,2,4<br/>
/// 如果Index是3那么对应的就是4号位角色，那么剩下三个按钮则是1,2,3
/// </summary>
public class IndexToButtonContentConverter : IValueConverter
{
    /// <summary>
    /// 按钮的索引位置。
    /// </summary>
    public int ButtonIndex { get; set; }

    /// <summary>
    /// 将控件索引转换为对应按钮的显示内容。
    /// </summary>
    /// <param name="value">控件当前索引</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>按钮应显示的序号，无法确定时返回 DoNothing</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var controlIndex = (int)value;
        var buttonContent = GetButtonContent(controlIndex);
        return buttonContent ?? Binding.DoNothing;
    }

    /// <summary>
    /// 获取按钮名称
    /// </summary>
    /// <param name="controlIndex">控件索引</param>
    /// <returns>按钮名称</returns>
    private int? GetButtonContent(int controlIndex)
    {
        switch (controlIndex)
        {
            case 0:
                return ButtonIndex switch
                {
                    1 => 2,
                    2 => 3,
                    3 => 4,
                    _ => null
                };
            case 1:
                return ButtonIndex switch
                {
                    1 => 1,
                    2 => 3,
                    3 => 4,
                    _ => null
                };
            case 2:
                return ButtonIndex switch
                {
                    1 => 1,
                    2 => 2,
                    3 => 4,
                    _ => null
                };
            case 3:
                return ButtonIndex switch
                {
                    1 => 1,
                    2 => 2,
                    3 => 3,
                    _ => null
                };
            default:
                return null;
        }
    }

    /// <summary>
    /// 不支持反向转换。
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>不支持</returns>
    /// <exception cref="NotImplementedException">始终抛出</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture
    )
    {
        throw new NotImplementedException();
    }
}