using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.ExamplePlugin;

/// <summary>
/// 示例插件 v3 前台控件，展示 <see cref="FrontedV3ControlBase"/> + <see cref="FrontedV3ControlAttribute"/>
/// + <see cref="FrontedV3Property{T}"/> + XAML 绑定 + 固定 Part 的最小闭环。
/// </summary>
/// <remarks>
/// <para>
/// 该控件通过 <c>[FrontedV3Control("TeamCard")]</c> 标注，注册后 Canonical Control Type 为
/// <c>plugin:plfjy.ExamplePlugin/TeamCard</c>。
/// </para>
/// <para>
/// 声明两个属性：
/// <list type="bullet">
/// <item><c>Appearance.TextColor</c>：颜色字符串，存储到 ExtensionData 的 <c>TextColor</c> 键，
/// 语义为 <see cref="FrontedV3PropertySemantic.Appearance"/>，参与 StyleTransfer 传播，
/// 默认值为 <c>White</c>。</item>
/// <item><c>Content.TeamName</c>：队伍名称，存储到 ExtensionData 的 <c>TeamName</c> 键，
/// 语义为 <see cref="FrontedV3PropertySemantic.Other"/>，不参与传播，默认值为 <c>Team</c>。</item>
/// </list>
/// </para>
/// <para>
/// 声明一个固定 Part <c>Logo</c>（<see cref="LogoPart"/>），能力为 <see cref="FrontedV3PartCapabilities.Resize"/>，
/// 宽高存储到 ExtensionData 的 <c>LogoWidth</c>/<c>LogoHeight</c> 键。XAML 中通过
/// <c>fronted:FrontedV3.PartId="Logo"</c> 将 <c>&lt;Image&gt;</c> 标记为该 Part 的 Visual。
/// </para>
/// <para>
/// Part 的运行时几何绑定由框架统一接管：<c>FrontedV3ControlHost</c> 在控件创建后调用
/// <c>FrontedV3PartVisualRuntimeBinder</c>，根据 Part Storage 中的 LogoWidth/LogoHeight
/// 自动应用到 LogoImage，派生控件无需在 <c>OnInitializeFrontedV3</c> 中手写几何读取代码。
/// </para>
/// <para>
/// 基类已在 <see cref="FrontedV3ControlBase.InitializeFrontedV3"/> 中将 <c>DataContext</c> 统一设置为
/// 完整 <see cref="FrontedV3ControlContext"/>，XAML 通过 <c>Options.*</c> 根命名空间访问 V3 属性
/// （例如 <c>{Binding Options.Content.TeamName}</c>），派生控件无需自行设置 DataContext。
/// </para>
/// </remarks>
[FrontedV3Control("TeamCard", DefaultWidth = 220, DefaultHeight = 64)]
public partial class TeamCardControl : FrontedV3ControlBase
{
    /// <summary>
    /// 文本颜色属性，逻辑路径 <c>Appearance.TextColor</c>，存储到 ExtensionData 的 <c>TextColor</c> 键，
    /// 语义为 <see cref="FrontedV3PropertySemantic.Appearance"/>，参与 StyleTransfer 传播，
    /// 默认值为 <c>White</c>。
    /// </summary>
    public static readonly FrontedV3Property<string> TextColorProperty =
        new("Appearance.TextColor", FrontedV3Storage.ExtensionData("TextColor"),
            new FrontedV3PropertyMetadata
            {
                DisplayNameKey = "TeamCardTextColor",
                Semantic = FrontedV3PropertySemantic.Appearance,
                DefaultValue = "White"
            });

    /// <summary>
    /// 队伍名称属性，逻辑路径 <c>Content.TeamName</c>，存储到 ExtensionData 的 <c>TeamName</c> 键，
    /// 语义为 <see cref="FrontedV3PropertySemantic.Other"/>，不参与 StyleTransfer 传播，
    /// 默认值为 <c>Team</c>。
    /// </summary>
    public static readonly FrontedV3Property<string> TeamNameProperty =
        new("Content.TeamName", FrontedV3Storage.ExtensionData("TeamName"),
            new FrontedV3PropertyMetadata
            {
                DisplayNameKey = "TeamCardTeamName",
                Semantic = FrontedV3PropertySemantic.Other,
                DefaultValue = "Team"
            });

    /// <summary>
    /// Logo 固定 Part 声明，标识为 <c>Logo</c>，能力为 <see cref="FrontedV3PartCapabilities.Resize"/>，
    /// 宽高分别存储到 ExtensionData 的 <c>LogoWidth</c> 与 <c>LogoHeight</c> 键。
    /// </summary>
    /// <remarks>
    /// XAML 中通过 <c>fronted:FrontedV3.PartId="Logo"</c> 将 <c>&lt;Image x:Name="LogoImage"/&gt;</c>
    /// 标记为该 Part 的 Visual；Designer 中用户可拖拽缩放该 Image，几何值持久化到 Config 的 ExtensionData。
    /// 运行时由 <c>FrontedV3PartVisualRuntimeBinder</c> 自动将 LogoWidth/LogoHeight 应用到 LogoImage。
    /// </remarks>
    public static readonly FrontedV3Part LogoPart =
        FrontedV3Part.Register<TeamCardControl>("Logo")
            .WithSize(
                FrontedV3Storage.ExtensionData("LogoWidth"),
                FrontedV3Storage.ExtensionData("LogoHeight"))
            .WithCapabilities(FrontedV3PartCapabilities.Resize);

    /// <summary>
    /// 初始化 <see cref="TeamCardControl"/> 并加载 XAML 组件。
    /// </summary>
    public TeamCardControl()
    {
        InitializeComponent();
    }
}
