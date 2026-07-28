using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 文本控件。
/// </summary>
[FrontedV3Control("Text", IsBuiltIn = true)]
public class TextFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not TextFrontedControlConfig textConfig)
        {
            throw new FrontedLayoutConfigException("Control config is not a Text config.");
        }

        var buildContext = context.ToBuildContext();
        var border = FrontedControlFactoryHelper.CreateBorderWithoutCanvasLayout(context.ControlName ?? string.Empty);
        var textBlock = new TextBlock();
        textBlock.Margin = new Thickness(
            textConfig.ContentMarginLeft,
            textConfig.ContentMarginTop,
            textConfig.ContentMarginRight,
            textConfig.ContentMarginBottom);

        if (textConfig.TextBinding?.GetActiveSources().Count > 0)
        {
            BindingOperations.SetBinding(
                textBlock,
                TextBlock.TextProperty,
                FrontedTextBindingHelper.CreateMultiBinding(textConfig.TextBinding, context.SharedDataService));
        }
        else if (textConfig.Text is not null)
        {
            textBlock.Text = textConfig.Text;
        }

        FrontedControlFactoryHelper.TryApplyEnum<HorizontalAlignment>(
            textConfig.HorizontalAlignment,
            value => textBlock.HorizontalAlignment = value,
            buildContext,
            nameof(textConfig.HorizontalAlignment));
        FrontedControlFactoryHelper.TryApplyEnum<VerticalAlignment>(
            textConfig.VerticalAlignment,
            value => textBlock.VerticalAlignment = value,
            buildContext,
            nameof(textConfig.VerticalAlignment));
        FrontedControlFactoryHelper.TryApplyEnum<TextAlignment>(
            textConfig.TextAlignment,
            value => textBlock.TextAlignment = value,
            buildContext,
            nameof(textConfig.TextAlignment));
        FrontedControlFactoryHelper.TryApplyEnum<TextWrapping>(
            textConfig.TextWrapping,
            value => textBlock.TextWrapping = value,
            buildContext,
            nameof(textConfig.TextWrapping));
        FrontedControlFactoryHelper.TryApplyTypeConverter<FontWeight>(
            textConfig.FontWeight,
            value => textBlock.FontWeight = value,
            buildContext,
            nameof(textConfig.FontWeight));
        FrontedTextForegroundBindingHelper.ApplyForeground(
            textBlock,
            textConfig.Color,
            textConfig.ColorBindingPath,
            buildContext,
            nameof(textConfig.Color));

        if (!string.IsNullOrWhiteSpace(textConfig.FontFamily))
        {
            textBlock.FontFamily = FrontedFontResourceHelper.CreateFontFamily(
                textConfig.FontFamily,
                context.ResourceResolver,
                context.Logger);
        }

        if (textConfig.FontSize > 0)
        {
            textBlock.FontSize = textConfig.FontSize;
        }

        border.Child = textBlock;
        Content = border;
    }
}
