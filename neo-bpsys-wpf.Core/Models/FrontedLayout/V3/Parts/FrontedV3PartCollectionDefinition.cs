using System.Collections;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

/// <summary>
/// PartCollection 的已解析定义，描述集合的标识、策略、项能力、集合访问方式与模板补齐逻辑。
/// </summary>
/// <remarks>
/// <para>
/// PartCollection 是控件内部可变子项集合的抽象（如 GlobalScoreRow 的 Cells）。
/// 与固定 <see cref="FrontedV3PartDefinition"/> 不同，PartCollection 的项数可变（受 <see cref="Strategy"/> 约束），
/// 每个项通过 <see cref="ItemKeySelector"/> 获取唯一键，几何值通过 <see cref="CollectionGetter"/> 返回的列表项上的属性读写。
/// </para>
/// <para>
/// <see cref="Strategy"/> 决定增删与模板补齐行为：
/// <list type="bullet">
/// <item><see cref="FrontedV3PartCollectionStrategy.FixedTemplate"/>：通过 <see cref="EnsureTemplateItems"/> 补齐缺失项，拒绝增删。</item>
/// <item><see cref="FrontedV3PartCollectionStrategy.Dynamic"/>：允许增删，不补齐。</item>
/// <item><see cref="FrontedV3PartCollectionStrategy.ReadOnly"/>：只读。</item>
/// </list>
/// </para>
/// <para>
/// <see cref="ItemCapabilities"/> 决定 Designer 中对单个集合项允许的几何操作类型；
/// <see cref="IFrontedV3GeometryTarget"/> 实现必须遵守能力约束。
/// </para>
/// <para>
/// 集合项的几何值直接读写列表项上的 CLR 属性（如 <c>X</c>/<c>Y</c>/<c>Width</c>/<c>Height</c>），
/// 不改变 JSON 结构，继续使用现有字段。
/// </para>
/// </remarks>
public sealed class FrontedV3PartCollectionDefinition
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3PartCollectionDefinition"/>。
    /// </summary>
    public FrontedV3PartCollectionDefinition()
    {
    }

    /// <summary>
    /// 初始化 <see cref="FrontedV3PartCollectionDefinition"/> 并指定全部属性。
    /// </summary>
    /// <param name="id">集合标识，在同一控件内必须唯一。</param>
    /// <param name="strategy">集合策略，决定增删与模板补齐行为。</param>
    /// <param name="itemCapabilities">单个集合项的操作能力。</param>
    /// <param name="collectionGetter">从 Config 获取集合列表的函数。</param>
    /// <param name="itemKeySelector">从集合项获取唯一键的函数。</param>
    /// <param name="ensureTemplateItems">对于 FixedTemplate 策略，补齐缺失模板项的回调；其他策略可为 <see langword="null"/>。</param>
    public FrontedV3PartCollectionDefinition(
        string id,
        FrontedV3PartCollectionStrategy strategy,
        FrontedV3PartCapabilities itemCapabilities,
        Func<FrontedControlConfigBase, IList> collectionGetter,
        Func<object, string> itemKeySelector,
        Action<FrontedControlConfigBase>? ensureTemplateItems = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        ItemCapabilities = itemCapabilities ?? throw new ArgumentNullException(nameof(itemCapabilities));
        CollectionGetter = collectionGetter ?? throw new ArgumentNullException(nameof(collectionGetter));
        ItemKeySelector = itemKeySelector ?? throw new ArgumentNullException(nameof(itemKeySelector));
        EnsureTemplateItems = ensureTemplateItems;
    }

    /// <summary>
    /// 获取或设置集合标识，在同一控件内必须唯一（例如 <c>Cells</c>）。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置集合策略，决定增删与模板补齐行为。
    /// </summary>
    public FrontedV3PartCollectionStrategy Strategy { get; set; } = FrontedV3PartCollectionStrategy.ReadOnly;

    /// <summary>
    /// 获取或设置单个集合项的操作能力，决定 Designer 中允许的几何操作类型。
    /// </summary>
    public FrontedV3PartCapabilities ItemCapabilities { get; set; } = FrontedV3PartCapabilities.MoveAndResize;

    /// <summary>
    /// 获取或设置从 Config 获取集合列表的函数。
    /// </summary>
    public Func<FrontedControlConfigBase, IList> CollectionGetter { get; set; } = _ => new List<object>();

    /// <summary>
    /// 获取或设置从集合项获取唯一键的函数，用于项的身份识别与选择恢复。
    /// </summary>
    public Func<object, string> ItemKeySelector { get; set; } = _ => string.Empty;

    /// <summary>
    /// 获取或设置对于 FixedTemplate 策略，补齐缺失模板项的回调；其他策略可为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 该回调只负责初始化与补齐，不负责 Designer 的几何操作。Designer 几何操作通过
    /// <see cref="neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry.CollectionItemGeometryTarget"/> 完成。
    /// </remarks>
    public Action<FrontedControlConfigBase>? EnsureTemplateItems { get; set; }

    /// <summary>
    /// 获取或设置集合项外观属性的工厂；为 <see langword="null"/> 时表示集合项仅支持几何编辑。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 当 PartCollection 集合项在 Designer 中被选中时，
    /// <see cref="neo_bpsys_wpf.Core.Services.FrontedLayout.V3.FrontedV3DesignSelectionBuilder.BuildCollectionItemSelection"/>
    /// 会将几何属性（<c>X</c>/<c>Y</c>/<c>Width</c>/<c>Height</c>）与该工厂返回的外观属性合并，
    /// 作为选中目标的 <c>Properties</c> 返回。
    /// </para>
    /// <para>
    /// 工厂参数 <paramref name="itemKey"/> 为选中集合项的唯一键（仅在选中时确定），
    /// 工厂返回的 <see cref="FrontedV3PropertyDefinition"/> 必须使用
    /// <see cref="FrontedV3Storage.CollectionItemProperty"/> 绑定到该 itemKey 对应的集合项 CLR 属性。
    /// </para>
    /// <para>
    /// 采用工厂而非预构建列表的原因：<see cref="FrontedV3Storage.CollectionItemProperty"/>
    /// 在构造时即需要 <c>itemKey</c> 参数，而 <c>itemKey</c> 仅在 Designer 选中具体集合项时才确定。
    /// 工厂模式将 Storage 构造延迟到选中时，避免引入额外描述类型。
    /// </para>
    /// <para>
    /// 默认 <see langword="null"/> 表示集合项只支持几何编辑。
    /// </para>
    /// </remarks>
    public Func<string, IReadOnlyList<FrontedV3PropertyDefinition>>? ItemPropertiesFactory { get; set; }

    /// <summary>
    /// 获取或设置根据控件自身模板（如 BO3/BO5、列表项间距等）重新分配集合项位置/可见性的回调；
    /// 为 <see langword="null"/> 时表示该集合不支持模板分配。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是通用机制，控件可通过此回调暴露"按模板重新分配布局"能力。
    /// 回调接收父控件 Config 与 <see cref="FrontedV3TemplateContext"/>，由控件自身实现决定如何分配位置与可见性。
    /// </para>
    /// <para>
    /// 例如 GlobalScoreRow 在此回调中调用
    /// <see cref="neo_bpsys_wpf.Core.Services.FrontedLayout.GlobalScoreRowCellLayoutHelper.AutoArrangeBySpacing"/>
    /// 与可见性模板方法，按 <see cref="FrontedV3TemplateContext.CurrentBoModeState"/> 或
    /// <see cref="FrontedV3TemplateContext.TemplateId"/> 指定的模板重新分配 Cell 的 <c>X</c>/<c>Y</c> 与 <c>Visibility</c>。
    /// </para>
    /// <para>
    /// 该回调只负责位置/可见性的模板分配，不修改外观属性（Color/FontFamily 等）。
    /// 回调返回 <see langword="true"/> 表示发生了修改；<see langword="false"/> 表示无变更。
    /// </para>
    /// <para>
    /// Designer 在选中根控件且该回调非 <see langword="null"/> 时显示"按模板重新分配"按钮：
    /// 当 <see cref="Templates"/> 非空时按模板逐个渲染；否则渲染单一通用按钮（无 <see cref="FrontedV3TemplateContext.TemplateId"/>）。
    /// </para>
    /// </remarks>
    public Func<FrontedControlConfigBase, FrontedV3TemplateContext, bool>? ApplyTemplate { get; set; }

    /// <summary>
    /// 获取或设置该集合支持的具名布局模板列表（如 BO3、BO5、Default、Compact）；
    /// 为空列表时 Designer 渲染单一通用"按模板重新分配"按钮，由控件基于
    /// <see cref="FrontedV3TemplateContext.CurrentBoModeState"/> 决定具体模板。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 非空列表时 Designer 不再渲染通用按钮，而是为每个 <see cref="FrontedV3LayoutTemplate"/> 渲染独立按钮，
    /// 用户点击后调用 <see cref="ApplyTemplate"/> 并通过 <see cref="FrontedV3TemplateContext.TemplateId"/>
    /// 传递被点击模板的 <see cref="FrontedV3LayoutTemplate.Id"/>。
    /// </para>
    /// <para>
    /// 列表为空但 <see cref="ApplyTemplate"/> 非 <see langword="null"/> 时，
    /// Designer 渲染单一通用按钮，<see cref="FrontedV3TemplateContext.TemplateId"/> 为 <see langword="null"/>。
    /// </para>
    /// <para>
    /// 默认为空列表，控件无需显式声明具名模板即可使用基于 BO 状态的默认行为。
    /// </para>
    /// </remarks>
    public IReadOnlyList<FrontedV3LayoutTemplate> Templates { get; set; } = Array.Empty<FrontedV3LayoutTemplate>();
}
