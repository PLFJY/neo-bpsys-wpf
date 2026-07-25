using System.Collections;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;

/// <summary>
/// 内置控件 PartCollection 定义解析器，为内置控件提供集合定义。
/// </summary>
/// <remarks>
/// <para>
/// GlobalScoreRow 等内置控件的 <c>Cells</c> 集合的几何操作走通用 <see cref="Geometry.CollectionItemGeometryTarget"/>。
/// 该解析器为这些控件提供 PartCollection 定义。
/// </para>
/// <para>
/// Collection Storage 映射到 Config 的现有集合字段，JSON 不变：
/// <list type="bullet">
/// <item>GlobalScoreRow.Cells：FixedTemplate 策略，项键=<c>Cell.Id</c>，几何属性=<c>X</c>/<c>Y</c>/<c>Width</c>/<c>Height</c>，
/// 模板补齐=<see cref="GlobalScoreRowCellLayoutHelper.EnsureCompleteCells(GlobalScoreRowControlConfig, bool)"/>（BO5 模板，确保全部 12 个 Cell 存在）。</item>
/// </list>
/// </para>
/// </remarks>
internal static class BuiltInPartCollectionDefinitionResolver
{
    /// <summary>
    /// 返回给定 Config 可用的 PartCollection 定义列表。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>集合定义列表；无可用集合时返回空列表。</returns>
    public static IReadOnlyList<FrontedV3PartCollectionDefinition> GetCollections(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config is GlobalScoreRowControlConfig)
        {
            return new[]
            {
                CreateGlobalScoreCellsCollection()
            };
        }

        return Array.Empty<FrontedV3PartCollectionDefinition>();
    }

    /// <summary>
    /// 返回给定 Config 是否有可用的 PartCollection。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>有可用集合时为 <see langword="true"/>。</returns>
    public static bool HasCollections(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config is GlobalScoreRowControlConfig;
    }

    /// <summary>
    /// 按 Id 查找给定 Config 的 PartCollection 定义。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <param name="collectionId">集合标识。</param>
    /// <returns>匹配的集合定义；未找到时为 <see langword="null"/>。</returns>
    public static FrontedV3PartCollectionDefinition? FindCollection(FrontedControlConfigBase config, string collectionId)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(collectionId);

        foreach (var collection in GetCollections(config))
        {
            if (string.Equals(collection.Id, collectionId, StringComparison.Ordinal))
            {
                return collection;
            }
        }

        return null;
    }

    private static FrontedV3PartCollectionDefinition CreateGlobalScoreCellsCollection()
    {
        var collectionGetter = new Func<FrontedControlConfigBase, IList>(
            config => ((GlobalScoreRowControlConfig)config).Cells);
        var itemKeySelector = new Func<object, string>(
            item => ((GlobalScoreCellConfig)item).Id);

        return new FrontedV3PartCollectionDefinition(
            id: "Cells",
            strategy: FrontedV3PartCollectionStrategy.FixedTemplate,
            itemCapabilities: FrontedV3PartCapabilities.MoveAndResize,
            collectionGetter: collectionGetter,
            itemKeySelector: itemKeySelector,
            ensureTemplateItems: config =>
                GlobalScoreRowCellLayoutHelper.EnsureCompleteCells(
                    (GlobalScoreRowControlConfig)config,
                    isBo3Mode: false))
        {
            // GlobalScoreRow Cell 外观属性 Schema。OptionsPath 与父级 GlobalScoreRowControlConfig
            // 同名属性保持一致（如 "FontFamily"），以便父到子派发按 OptionsPath 匹配。
            // 父级属性 OptionsPath 由 BuiltInPropertyDefinitionResolver.CreateDefinition 反射生成，
            // 取 property.Name（如 "FontFamily"/"Color"/"FontSize" 等），不添加 "Appearance." 前缀。
            // 因此子级外观属性 OptionsPath 也直接使用属性名，不添加前缀。
            ItemPropertiesFactory = itemKey => BuildGlobalScoreCellAppearanceProperties(
                collectionGetter, itemKeySelector, itemKey),
            // 暴露"按模板重新分配"能力：调用 BO5 模板补齐 + 可见性模板 + 间距自动排列。
            // 设计器在选中根控件且该回调非 null 时显示通用"按模板重新分配"按钮。
            // 这里固定使用 BO5 模板（与 EnsureTemplateItems 的 isBo3Mode: false 保持一致）。
            ApplyTemplate = config => ApplyGlobalScoreRowTemplate((GlobalScoreRowControlConfig)config)
        };
    }

    /// <summary>
    /// 对 GlobalScoreRow 应用 BO5 布局模板：补齐缺失 Cell、按 BO5 模板设置可见性、按间距自动排列位置。
    /// </summary>
    /// <param name="config">GlobalScoreRow 配置实例。</param>
    /// <returns>是否发生了修改。</returns>
    private static bool ApplyGlobalScoreRowTemplate(GlobalScoreRowControlConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        GlobalScoreRowCellLayoutHelper.ApplyBo5VisibilityTemplate(config);
        GlobalScoreRowCellLayoutHelper.AutoArrangeBySpacing(config, isBo3Mode: false);
        return true;
    }

    /// <summary>
    /// 构建 GlobalScoreRow Cell 的外观属性列表，使用
    /// <see cref="FrontedV3Storage.CollectionItemProperty"/> 绑定到指定 itemKey 对应的 Cell。
    /// </summary>
    /// <param name="collectionGetter">从 Config 获取 Cells 集合的函数。</param>
    /// <param name="itemKeySelector">从 Cell 获取唯一键的函数。</param>
    /// <param name="itemKey">选中 Cell 的唯一键。</param>
    /// <returns>外观属性定义列表，含 Color/FontFamily/FontWeight/FontSize/ShowCampIcon/CampIconColor/Visibility。</returns>
    private static IReadOnlyList<FrontedV3PropertyDefinition> BuildGlobalScoreCellAppearanceProperties(
        Func<FrontedControlConfigBase, IList> collectionGetter,
        Func<object, string> itemKeySelector,
        string itemKey)
    {
        return
        [
            CreateCellAppearanceProperty(
                collectionGetter, itemKeySelector, itemKey,
                propertyName: nameof(GlobalScoreCellConfig.Color),
                propertyType: typeof(string),
                editorKind: FrontedPropertyEditorKind.Color),
            CreateCellAppearanceProperty(
                collectionGetter, itemKeySelector, itemKey,
                propertyName: nameof(GlobalScoreCellConfig.FontFamily),
                propertyType: typeof(string),
                editorKind: FrontedPropertyEditorKind.FontFamily),
            CreateCellAppearanceProperty(
                collectionGetter, itemKeySelector, itemKey,
                propertyName: nameof(GlobalScoreCellConfig.FontWeight),
                propertyType: typeof(string),
                editorKind: FrontedPropertyEditorKind.Enum,
                options: BuildFontWeightOptions()),
            CreateCellAppearanceProperty(
                collectionGetter, itemKeySelector, itemKey,
                propertyName: nameof(GlobalScoreCellConfig.FontSize),
                propertyType: typeof(double?),
                editorKind: FrontedPropertyEditorKind.Number),
            CreateCellAppearanceProperty(
                collectionGetter, itemKeySelector, itemKey,
                propertyName: nameof(GlobalScoreCellConfig.ShowCampIcon),
                propertyType: typeof(bool?),
                editorKind: FrontedPropertyEditorKind.Boolean),
            CreateCellAppearanceProperty(
                collectionGetter, itemKeySelector, itemKey,
                propertyName: nameof(GlobalScoreCellConfig.CampIconColor),
                propertyType: typeof(GlobalScoreCampIconColor?),
                editorKind: FrontedPropertyEditorKind.Enum),
            CreateCellAppearanceProperty(
                collectionGetter, itemKeySelector, itemKey,
                propertyName: nameof(GlobalScoreCellConfig.Visibility),
                propertyType: typeof(FrontedControlVisibility),
                editorKind: FrontedPropertyEditorKind.Enum,
                inheritance: FrontedV3PropertyInheritance.None)
        ];
    }

    /// <summary>
    /// 创建单个 Cell 外观属性定义。除 <paramref name="inheritance"/> 显式指定为
    /// <see cref="FrontedV3PropertyInheritance.None"/> 外，默认使用
    /// <see cref="FrontedV3PropertyInheritance.ParentFallback"/>。
    /// </summary>
    /// <param name="collectionGetter">从 Config 获取 Cells 集合的函数。</param>
    /// <param name="itemKeySelector">从 Cell 获取唯一键的函数。</param>
    /// <param name="itemKey">选中 Cell 的唯一键。</param>
    /// <param name="propertyName">Cell 上的 CLR 属性名，同时作为 OptionsPath（与父级属性匹配）。</param>
    /// <param name="propertyType">属性的强类型（与 Cell CLR 属性类型一致）。</param>
    /// <param name="editorKind">属性网格使用的编辑器类型。</param>
    /// <param name="inheritance">属性继承模式；默认 <see cref="FrontedV3PropertyInheritance.ParentFallback"/>。</param>
    /// <param name="options">枚举属性的可选值列表；非枚举属性为 <see langword="null"/>。</param>
    /// <returns>构建好的 <see cref="FrontedV3PropertyDefinition"/>。</returns>
    private static FrontedV3PropertyDefinition CreateCellAppearanceProperty(
        Func<FrontedControlConfigBase, IList> collectionGetter,
        Func<object, string> itemKeySelector,
        string itemKey,
        string propertyName,
        Type propertyType,
        FrontedPropertyEditorKind editorKind,
        FrontedV3PropertyInheritance inheritance = FrontedV3PropertyInheritance.ParentFallback,
        IReadOnlyList<FrontedPropertyEditorOption>? options = null)
    {
        var storage = FrontedV3Storage.CollectionItemProperty(
            collectionGetter, itemKeySelector, itemKey, propertyName);

        var metadata = new FrontedV3PropertyMetadata
        {
            DisplayNameKey = propertyName,
            GroupName = "Appearance",
            EditorKind = editorKind,
            Semantic = FrontedV3PropertySemantic.Appearance,
            Inheritance = inheritance,
            Options = options
        };

        return new FrontedV3PropertyDefinition(
            optionsPath: propertyName,
            storage: storage,
            propertyType: propertyType,
            metadata: metadata);
    }

    /// <summary>
    /// 构建 FontWeight 字符串属性的可选值列表（与
    /// <see cref="Properties.BuiltInPropertyDefinitionResolver"/> 的 StringOptionProperties 保持一致）。
    /// </summary>
    /// <returns>FontWeight 可选值列表。</returns>
    private static IReadOnlyList<FrontedPropertyEditorOption> BuildFontWeightOptions()
    {
        return
        [
            new FrontedPropertyEditorOption { Value = "Normal", DisplayName = "Normal" },
            new FrontedPropertyEditorOption { Value = "Bold", DisplayName = "Bold" },
            new FrontedPropertyEditorOption { Value = "SemiBold", DisplayName = "SemiBold" },
            new FrontedPropertyEditorOption { Value = "Light", DisplayName = "Light" },
            new FrontedPropertyEditorOption { Value = "Medium", DisplayName = "Medium" },
            new FrontedPropertyEditorOption { Value = "ExtraBold", DisplayName = "ExtraBold" }
        ];
    }
}
