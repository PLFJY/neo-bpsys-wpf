using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Properties;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3;

/// <summary>
/// 构建 <see cref="FrontedV3DesignSelection"/> 的服务，根据设计项与子目标创建统一的选中目标。
/// </summary>
/// <remarks>
/// <para>
/// Phase 6 Designer 去特化的核心服务：所有 Root/FixedPart/CollectionItem selection 都通过该服务创建，
/// Designer ViewModel 不再通过 <c>if (config is BorderedImage...)</c> 等类型分支选择属性构造路径。
/// </para>
/// <para>
/// 该服务组合使用以下迁移期桥梁：
/// <list type="bullet">
/// <item><see cref="BuiltInPropertyDefinitionResolver"/>：为内置控件提供根属性 Schema。</item>
/// <item><see cref="BuiltInPartDefinitionResolver"/>：为内置控件提供固定 Part 定义。</item>
/// <item><see cref="BuiltInPartCollectionDefinitionResolver"/>：为内置控件提供 PartCollection 定义。</item>
/// <item><see cref="IFrontedV3ControlRegistry"/>：为 V3 注册的控件提供属性 Schema。</item>
/// </list>
/// </para>
/// <para>
/// Phase 7 内置控件迁移到 V3 后，迁移期桥梁将被删除，属性 Schema 统一由
/// <see cref="FrontedV3Property{T}"/> 声明并通过 Registry 查找。
/// </para>
/// <para>
/// 几何操作目标由该服务根据 selection 类别创建：
/// <list type="bullet">
/// <item>Root：<see cref="ConfigBackedRootGeometryTarget"/>（无 Host 依赖版本）。</item>
/// <item>FixedPart：<see cref="FixedPartGeometryTarget"/>。</item>
/// <item>CollectionItem：<see cref="CollectionItemGeometryTarget"/>。</item>
/// </list>
/// 所有 Move/Resize/Snap/Clamp/Undo 只通过 <see cref="IFrontedV3GeometryTarget"/> 调用。
/// </para>
/// </remarks>
public class FrontedV3DesignSelectionBuilder
{
    private readonly IFrontedV3ControlRegistry? _v3Registry;

    /// <summary>
    /// 使用默认配置初始化 <see cref="FrontedV3DesignSelectionBuilder"/>。
    /// </summary>
    public FrontedV3DesignSelectionBuilder()
        : this(null)
    {
    }

    /// <summary>
    /// 使用指定的 V3 注册表初始化 <see cref="FrontedV3DesignSelectionBuilder"/>。
    /// </summary>
    /// <param name="v3Registry">V3 控件注册表；为 <see langword="null"/> 时只支持内置控件。</param>
    public FrontedV3DesignSelectionBuilder(IFrontedV3ControlRegistry? v3Registry)
    {
        _v3Registry = v3Registry;
    }

    /// <summary>
    /// 为根控件创建选中目标。
    /// </summary>
    /// <param name="designItem">选中的设计项。</param>
    /// <param name="onVisualSync">可选的视觉同步回调，在几何值变更后由调用方触发视觉更新。</param>
    /// <returns>根控件选中目标；无可用 Schema 属性时返回 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="designItem"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3DesignSelection? BuildRootSelection(
        FrontedControlDesignItem designItem,
        Action? onVisualSync = null)
    {
        ArgumentNullException.ThrowIfNull(designItem);

        var properties = ResolveRootProperties(designItem.Config);
        if (properties.Count == 0)
        {
            return null;
        }

        var geometryTarget = new ConfigBackedRootGeometryTarget(designItem.Config, onVisualSync);
        return FrontedV3DesignSelection.ForRoot(designItem, geometryTarget, properties);
    }

    /// <summary>
    /// 为固定 Part 创建选中目标。
    /// </summary>
    /// <param name="designItem">父控件设计项。</param>
    /// <param name="partId">Part 标识。</param>
    /// <param name="onVisualSync">可选的视觉同步回调，在几何值变更后由调用方触发视觉更新。</param>
    /// <returns>固定 Part 选中目标；Part 不存在时返回 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="designItem"/> 或 <paramref name="partId"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <remarks>
    /// 查找顺序：优先从 Registry 中控件的 <see cref="FrontedV3ControlRegistration.FixedParts"/> 查找（统一链路，
    /// 覆盖插件与内置控件）；Registry 不可用时回退到 <see cref="BuiltInPartDefinitionResolver"/>（迁移期桥梁）。
    /// </remarks>
    public FrontedV3DesignSelection? BuildFixedPartSelection(
        FrontedControlDesignItem designItem,
        string partId,
        Action? onVisualSync = null)
    {
        ArgumentNullException.ThrowIfNull(designItem);
        ArgumentNullException.ThrowIfNull(partId);

        var part = FindPart(designItem.Config, partId);
        if (part is null)
        {
            return null;
        }

        var properties = BuildPartSelectionProperties(part);
        var geometryTarget = new FixedPartGeometryTarget(part, designItem.Config, onVisualSync);
        return FrontedV3DesignSelection.ForFixedPart(designItem, geometryTarget, properties, partId);
    }

    /// <summary>
    /// 为 PartCollection 集合项创建选中目标。
    /// </summary>
    /// <param name="designItem">父控件设计项。</param>
    /// <param name="collectionId">集合标识。</param>
    /// <param name="itemKey">集合项唯一键。</param>
    /// <param name="onVisualSync">可选的视觉同步回调，在几何值变更后由调用方触发视觉更新。</param>
    /// <returns>集合项选中目标；集合或项不存在时返回 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="designItem"/>、<paramref name="collectionId"/> 或 <paramref name="itemKey"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <remarks>
    /// 查找顺序：优先从 Registry 中控件的 <see cref="FrontedV3ControlRegistration.PartCollections"/> 查找（统一链路，
    /// 覆盖插件与内置控件）；Registry 不可用时回退到 <see cref="BuiltInPartCollectionDefinitionResolver"/>（迁移期桥梁）。
    /// </remarks>
    public FrontedV3DesignSelection? BuildCollectionItemSelection(
        FrontedControlDesignItem designItem,
        string collectionId,
        string itemKey,
        Action? onVisualSync = null)
    {
        ArgumentNullException.ThrowIfNull(designItem);
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(itemKey);

        var collection = FindCollection(designItem.Config, collectionId);
        if (collection is null)
        {
            return null;
        }

        var properties = BuildCollectionItemSelectionProperties(collection, itemKey);
        var geometryTarget = new CollectionItemGeometryTarget(collection, designItem.Config, itemKey, onVisualSync);
        return FrontedV3DesignSelection.ForCollectionItem(
            designItem,
            geometryTarget,
            properties,
            collectionId,
            itemKey);
    }

    /// <summary>
    /// 返回给定设计项可用的固定 Part 定义列表。
    /// </summary>
    /// <param name="designItem">设计项。</param>
    /// <returns>Part 定义列表；无可用 Part 时返回空列表。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="designItem"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <remarks>
    /// 查找顺序：优先从 Registry 中控件的 <see cref="FrontedV3ControlRegistration.FixedParts"/> 查找；
    /// Registry 不可用时回退到 <see cref="BuiltInPartDefinitionResolver"/>。
    /// </remarks>
    public IReadOnlyList<FrontedV3PartDefinition> GetAvailableParts(FrontedControlDesignItem designItem)
    {
        ArgumentNullException.ThrowIfNull(designItem);
        return GetPartsCore(designItem.Config);
    }

    /// <summary>
    /// 解析给定 Config 对应的 <see cref="FrontedV3ControlRegistration"/>。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>
    /// 匹配的注册信息；当 Registry 中存在时返回 Registry 注册项，
    /// 否则为内置控件按 <see cref="BuiltInPropertyDefinitionResolver"/> 反射生成的临时注册项。
    /// 当 Config 缺失或属性列表为空时返回 <see langword="null"/>。
    /// </returns>
    /// <remarks>
    /// <para>
    /// 该方法供 Designer 的 Peer Style Transfer 使用：源控件需要一个
    /// <see cref="FrontedV3ControlRegistration"/> 来描述 StyleTransfer 能力与属性 Schema。
    /// 对于插件控件，Registry 直接返回注册项；对于内置控件，若 Registry 未提供
    /// （例如测试场景），则按 <see cref="BuiltInPropertyDefinitionResolver"/> 反射生成
    /// 使用默认 <see cref="FrontedV3PropertyTransfer.Default"/> 能力的临时注册项。
    /// </para>
    /// <para>
    /// 临时注册项的 <see cref="FrontedV3ControlRegistration.ControlType"/> 与
    /// <see cref="FrontedV3ControlRegistration.ConfigType"/> 通过反射获取，
    /// 不引用具体内置控件类型，保持 Designer 去特化契约。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">当 <paramref name="config"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3ControlRegistration? ResolveRegistration(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (_v3Registry is not null
            && _v3Registry.GetRegistration(config.ControlType) is { } registryRegistration)
        {
            return registryRegistration;
        }

        var properties = BuiltInPropertyDefinitionResolver.GetProperties(config);
        if (properties.Count == 0)
        {
            return null;
        }

        var configType = config.GetType();
        var canonicalControlType = config.ControlType;
        return new FrontedV3ControlRegistration
        {
            CanonicalControlType = canonicalControlType,
            LocalControlId = canonicalControlType,
            PackageId = null,
            IsBuiltIn = true,
            ControlType = typeof(System.Windows.Controls.Border),
            ConfigType = configType,
            Properties = properties,
            CreateDefaultConfig = () =>
            {
                var instance = (FrontedControlConfigBase)Activator.CreateInstance(configType)!;
                instance.ControlType = canonicalControlType;
                return instance;
            }
        };
    }

    /// <summary>
    /// 返回给定设计项可用的 PartCollection 定义列表。
    /// </summary>
    /// <param name="designItem">设计项。</param>
    /// <returns>集合定义列表；无可用集合时返回空列表。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="designItem"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <remarks>
    /// 查找顺序：优先从 Registry 中控件的 <see cref="FrontedV3ControlRegistration.PartCollections"/> 查找；
    /// Registry 不可用时回退到 <see cref="BuiltInPartCollectionDefinitionResolver"/>。
    /// </remarks>
    public IReadOnlyList<FrontedV3PartCollectionDefinition> GetAvailableCollections(FrontedControlDesignItem designItem)
    {
        ArgumentNullException.ThrowIfNull(designItem);
        return GetCollectionsCore(designItem.Config);
    }

    /// <summary>
    /// 查找给定 Config 与 partId 的固定 Part 定义。优先从 Registration 查找，回退到内置 Resolver。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <param name="partId">Part 标识。</param>
    /// <returns>匹配的 Part 定义；未找到时为 <see langword="null"/>。</returns>
    /// <remarks>
    /// 该方法供 Designer ViewModel 在能力解析、几何补丁等场景复用统一查找链路，避免在 ViewModel 中直接依赖
    /// <see cref="BuiltInPartDefinitionResolver"/>。
    /// </remarks>
    public FrontedV3PartDefinition? FindPart(FrontedControlConfigBase config, string partId)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(partId);

        foreach (var part in GetPartsCore(config))
        {
            if (string.Equals(part.Id, partId, StringComparison.Ordinal))
            {
                return part;
            }
        }

        return null;
    }

    /// <summary>
    /// 查找给定 Config 与 collectionId 的 PartCollection 定义。优先从 Registration 查找，回退到内置 Resolver。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <param name="collectionId">集合标识。</param>
    /// <returns>匹配的集合定义；未找到时为 <see langword="null"/>。</returns>
    /// <remarks>
    /// 该方法供 Designer ViewModel 在能力解析、继承派发等场景复用统一查找链路，避免在 ViewModel 中直接依赖
    /// <see cref="BuiltInPartCollectionDefinitionResolver"/>。
    /// </remarks>
    public FrontedV3PartCollectionDefinition? FindCollection(FrontedControlConfigBase config, string collectionId)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(collectionId);

        foreach (var collection in GetCollectionsCore(config))
        {
            if (string.Equals(collection.Id, collectionId, StringComparison.Ordinal))
            {
                return collection;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取给定 Config 的固定 Part 列表。优先从 Registration 查找（统一链路），
    /// Registry 不可用时回退到 <see cref="BuiltInPartDefinitionResolver"/>（迁移期桥梁）。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>Part 定义列表。</returns>
    /// <remarks>
    /// 该方法供 Designer ViewModel 在几何补丁等场景复用统一查找链路。
    /// </remarks>
    public IReadOnlyList<FrontedV3PartDefinition> GetParts(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return GetPartsCore(config);
    }

    /// <summary>
    /// 获取给定 Config 的 PartCollection 列表。优先从 Registration 查找（统一链路），
    /// Registry 不可用时回退到 <see cref="BuiltInPartCollectionDefinitionResolver"/>（迁移期桥梁）。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>集合定义列表。</returns>
    /// <remarks>
    /// 该方法供 Designer ViewModel 在子控件派发、模板分配等场景复用统一查找链路。
    /// </remarks>
    public IReadOnlyList<FrontedV3PartCollectionDefinition> GetCollections(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return GetCollectionsCore(config);
    }

    private IReadOnlyList<FrontedV3PartDefinition> GetPartsCore(FrontedControlConfigBase config)
    {
        if (_v3Registry is not null
            && _v3Registry.GetRegistration(config.ControlType) is { } registration
            && registration.FixedParts.Count > 0)
        {
            return registration.FixedParts;
        }

        return BuiltInPartDefinitionResolver.GetParts(config);
    }

    private IReadOnlyList<FrontedV3PartCollectionDefinition> GetCollectionsCore(FrontedControlConfigBase config)
    {
        if (_v3Registry is not null
            && _v3Registry.GetRegistration(config.ControlType) is { } registration
            && registration.PartCollections.Count > 0)
        {
            return registration.PartCollections;
        }

        return BuiltInPartCollectionDefinitionResolver.GetCollections(config);
    }

    private IReadOnlyList<FrontedV3PropertyDefinition> ResolveRootProperties(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (_v3Registry is not null)
        {
            var registration = _v3Registry.GetRegistration(config.ControlType);
            if (registration is not null)
            {
                return registration.Properties;
            }
        }

        return BuiltInPropertyDefinitionResolver.GetProperties(config);
    }

    private static IReadOnlyList<FrontedV3PropertyDefinition> BuildPartGeometryProperties(FrontedV3PartDefinition part)
    {
        var properties = new List<FrontedV3PropertyDefinition>();

        if (part.XStorage is not null)
        {
            properties.Add(CreateGeometryProperty("X", part.XStorage));
        }

        if (part.YStorage is not null)
        {
            properties.Add(CreateGeometryProperty("Y", part.YStorage));
        }

        if (part.WidthStorage is not null)
        {
            properties.Add(CreateGeometryProperty("Width", part.WidthStorage));
        }

        if (part.HeightStorage is not null)
        {
            properties.Add(CreateGeometryProperty("Height", part.HeightStorage));
        }

        return properties;
    }

    /// <summary>
    /// 构建固定 Part 选中目标的属性列表，合并几何属性与 <see cref="FrontedV3PartDefinition.Properties"/>。
    /// </summary>
    /// <param name="part">Part 定义。</param>
    /// <returns>几何属性在前、外观属性在后的合并属性列表。</returns>
    private static IReadOnlyList<FrontedV3PropertyDefinition> BuildPartSelectionProperties(FrontedV3PartDefinition part)
    {
        var geometry = BuildPartGeometryProperties(part);
        if (part.Properties is null || part.Properties.Count == 0)
        {
            return geometry;
        }

        var combined = new List<FrontedV3PropertyDefinition>(geometry);
        combined.AddRange(part.Properties);
        return combined;
    }

    private static IReadOnlyList<FrontedV3PropertyDefinition> BuildCollectionItemGeometryProperties(
        FrontedV3PartCollectionDefinition collection,
        string itemKey)
    {
        return
        [
            CreateGeometryProperty("X", FrontedV3Storage.CollectionItemProperty(
                collection.CollectionGetter, collection.ItemKeySelector, itemKey, "X")),
            CreateGeometryProperty("Y", FrontedV3Storage.CollectionItemProperty(
                collection.CollectionGetter, collection.ItemKeySelector, itemKey, "Y")),
            CreateGeometryProperty("Width", FrontedV3Storage.CollectionItemProperty(
                collection.CollectionGetter, collection.ItemKeySelector, itemKey, "Width")),
            CreateGeometryProperty("Height", FrontedV3Storage.CollectionItemProperty(
                collection.CollectionGetter, collection.ItemKeySelector, itemKey, "Height"))
        ];
    }

    /// <summary>
    /// 构建集合项选中目标的属性列表，合并几何属性与
    /// <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/> 返回的外观属性。
    /// </summary>
    /// <param name="collection">集合定义。</param>
    /// <param name="itemKey">选中集合项的唯一键。</param>
    /// <returns>几何属性在前、外观属性在后的合并属性列表。</returns>
    private static IReadOnlyList<FrontedV3PropertyDefinition> BuildCollectionItemSelectionProperties(
        FrontedV3PartCollectionDefinition collection,
        string itemKey)
    {
        var geometry = BuildCollectionItemGeometryProperties(collection, itemKey);
        if (collection.ItemPropertiesFactory is null)
        {
            return geometry;
        }

        var appearance = collection.ItemPropertiesFactory(itemKey);
        if (appearance is null || appearance.Count == 0)
        {
            return geometry;
        }

        var combined = new List<FrontedV3PropertyDefinition>(geometry);
        combined.AddRange(appearance);
        return combined;
    }

    private static FrontedV3PropertyDefinition CreateGeometryProperty(
        string name,
        IFrontedV3StorageAccessor storage)
    {
        return new FrontedV3PropertyDefinition(
            optionsPath: name,
            storage: storage,
            propertyType: typeof(double),
            metadata: new FrontedV3PropertyMetadata
            {
                DisplayNameKey = name,
                GroupName = "Layout",
                EditorKind = FrontedPropertyEditorKind.Number
            });
    }
}
