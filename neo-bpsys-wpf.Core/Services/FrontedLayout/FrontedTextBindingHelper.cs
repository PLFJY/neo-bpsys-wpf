using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 文本绑定表达式的共享运行时和验证帮助程序。
/// </summary>
public static class FrontedTextBindingHelper
{
    /// <summary>
    /// 创建 Text 和 LocalizedText 控件使用的 MultiBinding。
    /// </summary>
    public static MultiBinding CreateMultiBinding(
        FrontedTextBindingExpression expression,
        ISharedDataService sharedDataService)
    {
        var multiBinding = new MultiBinding
        {
            Converter = new FrontedTextMultiBindingConverter(),
            ConverterParameter = expression,
            Mode = BindingMode.OneWay
        };

        foreach (var source in expression.GetActiveSources())
        {
            multiBinding.Bindings.Add(FrontedBindingFactory.Create(source.Path, sharedDataService));
        }

        return multiBinding;
    }

    /// <summary>
    /// 检查源数量的复合格式语法和占位符索引。
    /// </summary>
    public static bool TryValidateStringFormat(string? format, int sourceCount, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(format))
        {
            return true;
        }

        try
        {
            _ = string.Format(CultureInfo.InvariantCulture, format, new object?[sourceCount]);
            return true;
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
