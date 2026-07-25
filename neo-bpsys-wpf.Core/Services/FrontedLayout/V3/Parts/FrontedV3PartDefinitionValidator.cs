using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;

/// <summary>
/// 校验 <see cref="FrontedV3PartDefinition"/> 与 <see cref="FrontedV3PartCollectionDefinition"/>
/// 列表的声明完整性，在控件注册时 fail-fast 阻止非法声明进入 Registration。
/// </summary>
/// <remarks>
/// <para>
/// 该校验器在插件控件注册（<c>AddFrontedV3Control&lt;T&gt;</c>）与内置控件注册
/// （<c>AddBuiltInFrontedV3Control&lt;TControl,TConfig&gt;</c>）路径中共享调用，
/// 确保两条注册链路对 Part/PartCollection 的约束保持一致。
/// </para>
/// <para>
/// 校验规则：
/// <list type="bullet">
/// <item>Part Id 与 PartCollection Id 必须非空、非空白、不含路径分隔符或文件系统非法字符。</item>
/// <item>同一控件内的 Part Id 之间必须唯一（区分大小写）。</item>
/// <item>同一控件内的 PartCollection Id 之间必须唯一（区分大小写）。</item>
/// <item>Part Id 与 PartCollection Id 之间不得冲突（避免 Designer 选中歧义）。</item>
/// <item><see cref="FrontedV3PartCapabilities.CanMove"/> 为 <see langword="true"/> 时，
/// <see cref="FrontedV3PartDefinition.XStorage"/> 与 <see cref="FrontedV3PartDefinition.YStorage"/>
/// 不得同时为 <see langword="null"/>。</item>
/// <item><see cref="FrontedV3PartCapabilities.CanResize"/> 为 <see langword="true"/> 时，
/// <see cref="FrontedV3PartDefinition.WidthStorage"/> 与 <see cref="FrontedV3PartDefinition.HeightStorage"/>
/// 不得同时为 <see langword="null"/>。</item>
/// <item><see cref="FrontedV3PartCollectionStrategy.FixedTemplate"/> 策略的 PartCollection
/// 必须配置 <see cref="FrontedV3PartCollectionDefinition.EnsureTemplateItems"/>。</item>
/// <item>声明了具名 <see cref="FrontedV3PartCollectionDefinition.Templates"/> 的 PartCollection
/// 必须同时配置 <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/>。</item>
/// </list>
/// </para>
/// </remarks>
public static class FrontedV3PartDefinitionValidator
{
    /// <summary>
    /// 校验固定 Part 列表与 PartCollection 列表的声明完整性，包括跨集合 Id 唯一性。
    /// </summary>
    /// <param name="parts">固定 Part 定义列表。</param>
    /// <param name="collections">PartCollection 定义列表。</param>
    /// <param name="controlType">控件类型，用于错误消息。</param>
    /// <exception cref="FrontedLayoutConfigException">当任何 Part 或 PartCollection 声明违反约束时抛出。</exception>
    public static void Validate(
        IReadOnlyList<FrontedV3PartDefinition> parts,
        IReadOnlyList<FrontedV3PartCollectionDefinition> collections,
        Type controlType)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(controlType);

        ValidateParts(parts, controlType);
        ValidatePartCollections(collections, controlType);
        ValidateCrossCollectionIdUniqueness(parts, collections, controlType);
    }

    /// <summary>
    /// 校验固定 Part 列表的 Id 合法性、唯一性与 Capabilities/Storage 配对。
    /// </summary>
    /// <param name="parts">Part 定义列表。</param>
    /// <param name="controlType">控件类型，用于错误消息。</param>
    /// <exception cref="FrontedLayoutConfigException">当 Part 声明违反约束时抛出。</exception>
    public static void ValidateParts(IReadOnlyList<FrontedV3PartDefinition> parts, Type controlType)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(controlType);

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var part in parts)
        {
            EnsureValidPartId(part.Id, controlType);

            if (!seen.Add(part.Id))
            {
                throw new FrontedLayoutConfigException(
                    $"Control '{controlType.FullName}' has duplicate Part Id '{part.Id}'. " +
                    "Each Part Id must be unique within a control.");
            }

            ValidatePartCapabilitiesStoragePairing(part, controlType);
        }
    }

    /// <summary>
    /// 校验 PartCollection 列表的 Id 合法性、唯一性、策略/回调配对与 Templates/ApplyTemplate 配对。
    /// </summary>
    /// <param name="collections">PartCollection 定义列表。</param>
    /// <param name="controlType">控件类型，用于错误消息。</param>
    /// <exception cref="FrontedLayoutConfigException">当 PartCollection 声明违反约束时抛出。</exception>
    public static void ValidatePartCollections(
        IReadOnlyList<FrontedV3PartCollectionDefinition> collections,
        Type controlType)
    {
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(controlType);

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var collection in collections)
        {
            EnsureValidPartId(collection.Id, controlType, isCollection: true);

            if (!seen.Add(collection.Id))
            {
                throw new FrontedLayoutConfigException(
                    $"Control '{controlType.FullName}' has duplicate PartCollection Id '{collection.Id}'. " +
                    "Each PartCollection Id must be unique within a control.");
            }

            ValidateCollectionStrategyPairing(collection, controlType);
            ValidateCollectionTemplatesPairing(collection, controlType);
        }
    }

    /// <summary>
    /// 校验 Part Id 与 PartCollection Id 之间不得冲突。
    /// </summary>
    /// <param name="parts">固定 Part 定义列表。</param>
    /// <param name="collections">PartCollection 定义列表。</param>
    /// <param name="controlType">控件类型，用于错误消息。</param>
    /// <exception cref="FrontedLayoutConfigException">当 Part Id 与 PartCollection Id 冲突时抛出。</exception>
    public static void ValidateCrossCollectionIdUniqueness(
        IReadOnlyList<FrontedV3PartDefinition> parts,
        IReadOnlyList<FrontedV3PartCollectionDefinition> collections,
        Type controlType)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(controlType);

        if (parts.Count == 0 || collections.Count == 0)
        {
            return;
        }

        var partIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            partIds.Add(part.Id);
        }

        foreach (var collection in collections)
        {
            if (partIds.Contains(collection.Id))
            {
                throw new FrontedLayoutConfigException(
                    $"Control '{controlType.FullName}' has a PartCollection Id '{collection.Id}' " +
                    "that conflicts with an existing Part Id. " +
                    "Part Id and PartCollection Id must not overlap within a control.");
            }
        }
    }

    private static void EnsureValidPartId(string id, Type controlType, bool isCollection = false)
    {
        var kindLabel = isCollection ? "PartCollection Id" : "Part Id";

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new FrontedLayoutConfigException(
                $"Control '{controlType.FullName}' has a {kindLabel} that is null, empty, or whitespace. " +
                $"Each {kindLabel} must be a non-empty, non-whitespace string.");
        }

        if (id.Contains('/') || id.Contains('\\') || id.Contains(':'))
        {
            throw new FrontedLayoutConfigException(
                $"Control '{controlType.FullName}' has {kindLabel} '{id}' that contains path separators " +
                "('/', '\\', ':'). Part and PartCollection Ids must be simple identifiers.");
        }

        if (id.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new FrontedLayoutConfigException(
                $"Control '{controlType.FullName}' has {kindLabel} '{id}' that contains invalid file name characters. " +
                "Part and PartCollection Ids must be valid file name compatible identifiers.");
        }
    }

    private static void ValidatePartCapabilitiesStoragePairing(
        FrontedV3PartDefinition part,
        Type controlType)
    {
        if (part.Capabilities.CanMove
            && part.XStorage is null
            && part.YStorage is null)
        {
            throw new FrontedLayoutConfigException(
                $"Control '{controlType.FullName}' Part '{part.Id}' declares CanMove=true " +
                "but both XStorage and YStorage are null. " +
                "A movable Part must configure at least one of XStorage or YStorage.");
        }

        if (part.Capabilities.CanResize
            && part.WidthStorage is null
            && part.HeightStorage is null)
        {
            throw new FrontedLayoutConfigException(
                $"Control '{controlType.FullName}' Part '{part.Id}' declares CanResize=true " +
                "but both WidthStorage and HeightStorage are null. " +
                "A resizable Part must configure at least one of WidthStorage or HeightStorage.");
        }
    }

    private static void ValidateCollectionStrategyPairing(
        FrontedV3PartCollectionDefinition collection,
        Type controlType)
    {
        if (collection.Strategy == FrontedV3PartCollectionStrategy.FixedTemplate
            && collection.EnsureTemplateItems is null)
        {
            throw new FrontedLayoutConfigException(
                $"Control '{controlType.FullName}' PartCollection '{collection.Id}' uses " +
                "FixedTemplate strategy but EnsureTemplateItems is null. " +
                "A FixedTemplate PartCollection must configure EnsureTemplateItems to guarantee template items exist.");
        }
    }

    private static void ValidateCollectionTemplatesPairing(
        FrontedV3PartCollectionDefinition collection,
        Type controlType)
    {
        if (collection.Templates.Count > 0 && collection.ApplyTemplate is null)
        {
            throw new FrontedLayoutConfigException(
                $"Control '{controlType.FullName}' PartCollection '{collection.Id}' declares " +
                $"{collection.Templates.Count} named Templates but ApplyTemplate is null. " +
                "A PartCollection with named Templates must configure ApplyTemplate to handle template activation.");
        }
    }
}
