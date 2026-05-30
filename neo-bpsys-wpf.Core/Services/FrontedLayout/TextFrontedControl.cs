using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 文本控件工厂。
/// </summary>
public class TextFrontedControl : IFrontedControl
{
    /// <inheritdoc />
    public string ControlType => "Text";

    /// <inheritdoc />
    public Type ConfigType => typeof(TextFrontedControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not TextFrontedControlConfig textConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a Text config.");
        }

        var border = FrontedControlFactoryHelper.CreateOuterBorder(name, textConfig);
        var textBlock = new TextBlock();

        if (!string.IsNullOrWhiteSpace(textConfig.BindingPath))
        {
            BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, new Binding(textConfig.BindingPath)
            {
                Source = context.SharedDataService
            });
        }

        FrontedControlFactoryHelper.TryApplyEnum<HorizontalAlignment>(
            textConfig.HorizontalAlignment,
            value => textBlock.HorizontalAlignment = value,
            context,
            nameof(textConfig.HorizontalAlignment));
        FrontedControlFactoryHelper.TryApplyEnum<VerticalAlignment>(
            textConfig.VerticalAlignment,
            value => textBlock.VerticalAlignment = value,
            context,
            nameof(textConfig.VerticalAlignment));
        FrontedControlFactoryHelper.TryApplyEnum<TextAlignment>(
            textConfig.TextAlignment,
            value => textBlock.TextAlignment = value,
            context,
            nameof(textConfig.TextAlignment));
        FrontedControlFactoryHelper.TryApplyEnum<TextWrapping>(
            textConfig.TextWrapping,
            value => textBlock.TextWrapping = value,
            context,
            nameof(textConfig.TextWrapping));
        FrontedControlFactoryHelper.TryApplyTypeConverter<FontWeight>(
            textConfig.FontWeight,
            value => textBlock.FontWeight = value,
            context,
            nameof(textConfig.FontWeight));
        FrontedControlFactoryHelper.TryApplyTypeConverter<Brush>(
            textConfig.Color,
            value => textBlock.Foreground = value,
            context,
            nameof(textConfig.Color));

        if (!string.IsNullOrWhiteSpace(textConfig.FontFamily))
        {
            if (textConfig.FontFamily.Contains("pack://application:,,,"))
                textBlock.FontFamily = new FontFamily(
                    new Uri(textConfig.FontFamily[..textConfig.FontFamily.IndexOf('#')]),
                    "./" + textConfig.FontFamily[textConfig.FontFamily.IndexOf('#')..]);
            else
            {
                textBlock.FontFamily = new FontFamily(textConfig.FontFamily);
            }
        }

        if (textConfig.FontSize > 0)
        {
            textBlock.FontSize = textConfig.FontSize;
        }

        border.Child = textBlock;
        return border;
    }
}
