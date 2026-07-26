using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Helpers;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 对局进度到文字转换器。
/// </summary>
public class GameProgressToStringConverter : IMultiValueConverter
{
    /// <summary>
    /// 将对局进度转换为本地化文本。
    /// </summary>
    /// <param name="values">values[0] 为 GameProgress 对局进度，values[1] 为是否 Bo3 模式的布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>格式化后的对局进度文本，输入无效时返回 DoNothing</returns>
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values[0] is not GameProgress gameProgress || values[1] is not bool isBo3Mode)
        {
            return Binding.DoNothing;
        }

        var text = GameProgressDisplayHelper.Format(
            gameProgress,
            isBo3Mode);

        return string.IsNullOrEmpty(text) ? Binding.DoNothing : text;
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
    public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
