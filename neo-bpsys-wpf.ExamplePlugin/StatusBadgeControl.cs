using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.PluginSdk;

namespace neo_bpsys_wpf.ExamplePlugin;

/// <summary>
/// 示例插件 v3 前台控件，展示纯 C#（无 XAML）方式构建视觉树、声明相同 Attribute、
/// 相同 Property API 与相同 Options 绑定。
/// </summary>
/// <remarks>
/// <para>
/// 该控件通过 <c>[FrontedV3Control("StatusBadge")]</c> 标注，注册后 Canonical Control Type 为
/// <c>plugin:plfjy.ExamplePlugin/StatusBadge</c>。
/// </para>
/// <para>
/// 声明两个属性：
/// <list type="bullet">
/// <item><c>Appearance.BadgeColor</c>：徽章背景颜色字符串，存储到 ExtensionData 的 <c>BadgeColor</c> 键，
/// 语义为 <see cref="FrontedV3PropertySemantic.Appearance"/>，参与 StyleTransfer 传播。</item>
/// <item><c>Content.StatusText</c>：徽章文本，存储到 ExtensionData 的 <c>StatusText</c> 键，
/// 语义为 <see cref="FrontedV3PropertySemantic.Other"/>，不参与传播。</item>
/// </list>
/// </para>
/// <para>
/// 与 <see cref="TeamCardControl"/> 不同，该控件不使用 XAML，而是在
/// <see cref="OnInitializeFrontedV3"/> 中以纯 C# 构建 <see cref="Border"/> + <see cref="TextBlock"/>
/// 视觉树，并通过 <see cref="BindingOperations.SetBinding"/> 建立与 Options 视图的绑定。
/// </para>
/// </remarks>
[FrontedV3Control("StatusBadge")]
public class StatusBadgeControl : FrontedV3ControlBase
{
    /// <summary>
    /// 徽章背景颜色属性，逻辑路径 <c>Appearance.BadgeColor</c>，存储到 ExtensionData 的 <c>BadgeColor</c> 键，
    /// 语义为 <see cref="FrontedV3PropertySemantic.Appearance"/>，参与 StyleTransfer 传播。
    /// </summary>
    public static readonly FrontedV3Property<string> BadgeColorProperty =
        new("Appearance.BadgeColor", FrontedV3Storage.ExtensionData("BadgeColor"),
            new FrontedV3PropertyMetadata { Semantic = FrontedV3PropertySemantic.Appearance });

    /// <summary>
    /// 徽章文本属性，逻辑路径 <c>Content.StatusText</c>，存储到 ExtensionData 的 <c>StatusText</c> 键，
    /// 语义为 <see cref="FrontedV3PropertySemantic.Other"/>，不参与 StyleTransfer 传播。
    /// </summary>
    public static readonly FrontedV3Property<string> StatusTextProperty =
        new("Content.StatusText", FrontedV3Storage.ExtensionData("StatusText"),
            new FrontedV3PropertyMetadata { Semantic = FrontedV3PropertySemantic.Other });

    /// <summary>
    /// 初始化 <see cref="StatusBadgeControl"/>。该控件不使用 XAML，视觉树在
    /// <see cref="OnInitializeFrontedV3"/> 中以纯 C# 构建。
    /// </summary>
    public StatusBadgeControl()
    {
    }

    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        DataContext = context.Options;

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var textBlock = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var textBinding = new Binding("Content.StatusText");
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, textBinding);

        var backgroundBinding = new Binding("Appearance.BadgeColor")
        {
            Converter = new StringToBrushConverter()
        };
        BindingOperations.SetBinding(border, Border.BackgroundProperty, backgroundBinding);

        border.Child = textBlock;
        Content = border;
    }
}
