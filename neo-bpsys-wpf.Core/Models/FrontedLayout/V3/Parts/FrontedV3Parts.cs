using System.Collections;
using System.Reflection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

/// <summary>
/// v3 前台控件 PartCollection 的声明，作为控件类上的 <c>public static readonly</c> 字段使用。
/// </summary>
/// <remarks>
/// <para>
/// 典型用法：
/// </para>
/// <code>
/// public static readonly FrontedV3Parts CellsCollection =
///     FrontedV3Parts.RegisterCollection&lt;GlobalScoreRowControl&gt;("Cells")
///         .WithStrategy(FrontedV3PartCollectionStrategy.FixedTemplate)
///         .WithItemCapabilities(FrontedV3PartCapabilities.MoveAndResize)
///         .WithCollectionGetter(c => ((GlobalScoreRowControlConfig)c).Cells)
///         .WithItemKeySelector(item => ((GlobalScoreCellConfig)item).Id)
///         .WithEnsureTemplateItems(c => EnsureCells((GlobalScoreRowControlConfig)c));
/// </code>
/// <para>
/// 框架在注册控件时通过反射发现这些字段，转换为 <see cref="FrontedV3PartCollectionDefinition"/>。
/// </para>
/// <para>
/// 对于尚未迁移到 V3 注册的内置控件（如 GlobalScoreRow），使用
/// <c>BuiltInPartCollectionDefinitionResolver</c> 提供集合定义，作为迁移期的 internal 桥梁。
/// </para>
/// </remarks>
public sealed class FrontedV3Parts
{
    private readonly FrontedV3PartCollectionDefinition _definition;

    private FrontedV3Parts(FrontedV3PartCollectionDefinition definition)
    {
        _definition = definition;
    }

    /// <summary>
    /// 获取该集合声明的控件类型。
    /// </summary>
    public Type ControlType { get; private set; } = null!;

    /// <summary>
    /// 获取集合标识。
    /// </summary>
    public string Id => _definition.Id;

    /// <summary>
    /// 获取集合策略。
    /// </summary>
    public FrontedV3PartCollectionStrategy Strategy => _definition.Strategy;

    /// <summary>
    /// 获取集合项的操作能力。
    /// </summary>
    public FrontedV3PartCapabilities ItemCapabilities => _definition.ItemCapabilities;

    /// <summary>
    /// 开始为指定控件类型注册 PartCollection。
    /// </summary>
    /// <typeparam name="TControl">控件类型。</typeparam>
    /// <param name="id">集合标识，在同一控件内必须唯一。</param>
    /// <returns>用于链式配置的 <see cref="FrontedV3Parts"/> 实例。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="id"/> 为 <see langword="null"/> 时抛出。</exception>
    public static FrontedV3Parts RegisterCollection<TControl>(string id) where TControl : class
    {
        ArgumentNullException.ThrowIfNull(id);
        return new FrontedV3Parts(new FrontedV3PartCollectionDefinition
        {
            Id = id,
            Strategy = FrontedV3PartCollectionStrategy.Dynamic,
            ItemCapabilities = FrontedV3PartCapabilities.MoveAndResize
        })
        {
            ControlType = typeof(TControl)
        };
    }

    /// <summary>
    /// 设置集合策略。
    /// </summary>
    /// <param name="strategy">集合策略。</param>
    /// <returns>当前 <see cref="FrontedV3Parts"/> 实例，支持链式配置。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="strategy"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3Parts WithStrategy(FrontedV3PartCollectionStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _definition.Strategy = strategy;
        return this;
    }

    /// <summary>
    /// 设置集合项的操作能力。
    /// </summary>
    /// <param name="capabilities">集合项的操作能力。</param>
    /// <returns>当前 <see cref="FrontedV3Parts"/> 实例，支持链式配置。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="capabilities"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3Parts WithItemCapabilities(FrontedV3PartCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        _definition.ItemCapabilities = capabilities;
        return this;
    }

    /// <summary>
    /// 设置从 Config 获取集合列表的函数。
    /// </summary>
    /// <param name="collectionGetter">从 Config 获取集合列表的函数。</param>
    /// <returns>当前 <see cref="FrontedV3Parts"/> 实例，支持链式配置。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="collectionGetter"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3Parts WithCollectionGetter(Func<FrontedControlConfigBase, IList> collectionGetter)
    {
        ArgumentNullException.ThrowIfNull(collectionGetter);
        _definition.CollectionGetter = collectionGetter;
        return this;
    }

    /// <summary>
    /// 设置从集合项获取唯一键的函数。
    /// </summary>
    /// <param name="itemKeySelector">从集合项获取唯一键的函数。</param>
    /// <returns>当前 <see cref="FrontedV3Parts"/> 实例，支持链式配置。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="itemKeySelector"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3Parts WithItemKeySelector(Func<object, string> itemKeySelector)
    {
        ArgumentNullException.ThrowIfNull(itemKeySelector);
        _definition.ItemKeySelector = itemKeySelector;
        return this;
    }

    /// <summary>
    /// 设置 FixedTemplate 策略下补齐缺失模板项的回调。
    /// </summary>
    /// <param name="ensureTemplateItems">补齐缺失模板项的回调。</param>
    /// <returns>当前 <see cref="FrontedV3Parts"/> 实例，支持链式配置。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="ensureTemplateItems"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3Parts WithEnsureTemplateItems(Action<FrontedControlConfigBase> ensureTemplateItems)
    {
        ArgumentNullException.ThrowIfNull(ensureTemplateItems);
        _definition.EnsureTemplateItems = ensureTemplateItems;
        return this;
    }

    /// <summary>
    /// 设置集合项外观属性的工厂。工厂参数 <paramref name="itemKey"/> 为选中集合项的唯一键，
    /// 工厂返回的属性定义应使用 <see cref="FrontedV3Storage.CollectionItemProperty"/> 绑定到该 itemKey 对应的项属性。
    /// </summary>
    /// <param name="itemPropertiesFactory">集合项外观属性工厂。</param>
    /// <returns>当前 <see cref="FrontedV3Parts"/> 实例，支持链式配置。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="itemPropertiesFactory"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3Parts WithItemPropertiesFactory(Func<string, IReadOnlyList<FrontedV3PropertyDefinition>> itemPropertiesFactory)
    {
        ArgumentNullException.ThrowIfNull(itemPropertiesFactory);
        _definition.ItemPropertiesFactory = itemPropertiesFactory;
        return this;
    }

    /// <summary>
    /// 设置按模板重新分配集合项位置/可见性的回调。
    /// </summary>
    /// <param name="applyTemplate">模板分配回调，接收父控件 Config 与 <see cref="FrontedV3TemplateContext"/>，返回是否发生修改。</param>
    /// <returns>当前 <see cref="FrontedV3Parts"/> 实例，支持链式配置。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="applyTemplate"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3Parts WithApplyTemplate(Func<FrontedControlConfigBase, FrontedV3TemplateContext, bool> applyTemplate)
    {
        ArgumentNullException.ThrowIfNull(applyTemplate);
        _definition.ApplyTemplate = applyTemplate;
        return this;
    }

    /// <summary>
    /// 设置该集合支持的具名布局模板列表（如 BO3、BO5、Default、Compact）。
    /// </summary>
    /// <param name="templates">具名模板列表；为空列表时 Designer 渲染单一通用按钮。</param>
    /// <returns>当前 <see cref="FrontedV3Parts"/> 实例，支持链式配置。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="templates"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3Parts WithTemplates(IEnumerable<FrontedV3LayoutTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        _definition.Templates = templates is IReadOnlyList<FrontedV3LayoutTemplate> list
            ? list
            : templates.ToArray();
        return this;
    }

    /// <summary>
    /// 将该集合声明转换为 <see cref="FrontedV3PartCollectionDefinition"/>。
    /// </summary>
    /// <returns>与该声明等价的 <see cref="FrontedV3PartCollectionDefinition"/>。</returns>
    /// <remarks>
    /// 返回的定义会复制 <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/>、
    /// <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 与
    /// <see cref="FrontedV3PartCollectionDefinition.Templates"/>，确保声明链路上配置的字段不会丢失。
    /// </remarks>
    public FrontedV3PartCollectionDefinition ToDefinition()
    {
        var copy = new FrontedV3PartCollectionDefinition(
            _definition.Id,
            _definition.Strategy,
            _definition.ItemCapabilities,
            _definition.CollectionGetter,
            _definition.ItemKeySelector,
            _definition.EnsureTemplateItems)
        {
            ItemPropertiesFactory = _definition.ItemPropertiesFactory,
            ApplyTemplate = _definition.ApplyTemplate,
            Templates = _definition.Templates
        };
        return copy;
    }

    /// <summary>
    /// 从控件类型上发现所有 <c>public static readonly FrontedV3Parts</c> 字段并转换为定义列表。
    /// </summary>
    /// <param name="controlType">控件类型。</param>
    /// <returns>该控件声明的所有 PartCollection 定义列表。</returns>
    public static IReadOnlyList<FrontedV3PartCollectionDefinition> Discover(Type controlType)
    {
        ArgumentNullException.ThrowIfNull(controlType);
        var fields = controlType.GetFields(BindingFlags.Public | BindingFlags.Static);
        var definitions = new List<FrontedV3PartCollectionDefinition>();

        foreach (var field in fields)
        {
            if (field.FieldType != typeof(FrontedV3Parts))
            {
                continue;
            }

            if (field.GetValue(null) is not FrontedV3Parts collection)
            {
                continue;
            }

            definitions.Add(collection.ToDefinition());
        }

        return definitions;
    }
}
