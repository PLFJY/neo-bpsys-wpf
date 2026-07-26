using System.Globalization;

namespace neo_bpsys_wpf.Core.Events;

/// <summary>
/// 语言变更事件参数
/// </summary>
/// <param name="cultureInfo">变更后的语言区域信息</param>
public class LanguageChangedEventArgs(CultureInfo cultureInfo) : EventArgs
{
    /// <summary>
    /// 变更后的语言区域信息
    /// </summary>
    public CultureInfo CultureInfo { get; } = cultureInfo;
}