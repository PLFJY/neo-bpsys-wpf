using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Shared runtime and validation helpers for text binding expressions.
/// </summary>
public static class FrontedTextBindingHelper
{
    /// <summary>
    /// Creates the MultiBinding used by Text and LocalizedText controls.
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
            multiBinding.Bindings.Add(new System.Windows.Data.Binding(source.Path)
            {
                Source = sharedDataService,
                Mode = BindingMode.OneWay
            });
        }

        return multiBinding;
    }

    /// <summary>
    /// Checks composite format syntax and placeholder indexes for the source count.
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
