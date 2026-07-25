using System.Text.Json;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.StyleTransfer;

/// <summary>
/// v3 前台控件 StyleTransfer 核心服务，执行父-子继承与同 peer 传播。
/// </summary>
/// <remarks>
/// <para>
/// 该服务替代旧链路中 MapV2/GlobalScore 的手写 StyleTransfer 特判：
/// <list type="bullet">
/// <item>Peer 传播（替代 <c>ApplyMapV2DisplayStyleToAll</c>/<c>CopyMapV2DisplayStyle</c>）：仅匹配完全相同的 <see cref="FrontedV3ControlRegistration.CanonicalControlType"/>。</item>
/// <item>Parent-Child 继承（替代 <c>ApplyParentStyleToGlobalScoreCells</c>/<c>ClearGlobalScoreCellStyleOverrides</c>）：根据相同 OptionsPath 匹配父子属性。</item>
/// </list>
/// </para>
/// <para>
/// <b>不可破坏的传播约束</b>：
/// <list type="bullet">
/// <item>默认仅传播 <see cref="FrontedV3PropertySemantic.Appearance"/> 语义的属性。</item>
/// <item><see cref="FrontedV3PropertySemantic.RootSize"/>/<see cref="FrontedV3PropertySemantic.PartLayout"/>/<see cref="FrontedV3PropertySemantic.Behaviors"/>/<see cref="FrontedV3PropertySemantic.Effects"/> 只有 profile 显式开启时才传播。</item>
/// <item><see cref="FrontedV3PropertySemantic.DataIdentity"/> 和 <see cref="FrontedV3PropertySemantic.Other"/> 永远不传播（数据身份字段、MapKey、TeamType、BindingPath、ControlName 等）。</item>
/// <item>根级保留字段（<c>Left</c>/<c>Top</c>/<c>ZIndex</c> 等）不会注册为属性，因此不在传播范围内。</item>
/// </list>
/// </para>
/// <para>
/// <b>ParentFallback 动态读取</b>：<see cref="ReadValueWithInheritance"/> 每次调用时动态判断子控件是否有 override，
/// 不缓存 fallback 值。父控件后续修改会反映到子控件的读取结果。
/// </para>
/// </remarks>
public sealed class FrontedV3StyleTransferService
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3StyleTransferService"/>。
    /// </summary>
    public FrontedV3StyleTransferService()
    {
    }

    /// <summary>
    /// 按继承模式读取子控件的属性值，动态应用 <see cref="FrontedV3PropertyInheritance.ParentFallback"/> 与 <see cref="FrontedV3PropertyInheritance.LockedToParent"/> 语义。
    /// </summary>
    /// <param name="property">子控件的属性定义（包含继承模式元数据）。</param>
    /// <param name="childConfig">子控件配置实例。</param>
    /// <param name="parentConfig">父控件配置实例；为 <see langword="null"/> 时按无父处理。</param>
    /// <param name="parentProperty">父控件中与子属性同 OptionsPath 的属性定义；为 <see langword="null"/> 时按无父处理。</param>
    /// <returns>按继承模式解析后的属性值。</returns>
    /// <remarks>
    /// <para>
    /// 继承模式处理规则：
    /// <list type="bullet">
    /// <item><see cref="FrontedV3PropertyInheritance.None"/>：直接读取子控件值。</item>
    /// <item><see cref="FrontedV3PropertyInheritance.ParentFallback"/>：先读取子控件 override；override 为 <see langword="null"/> 时动态回退到父控件同 OptionsPath 的值。不缓存 fallback。</item>
    /// <item><see cref="FrontedV3PropertyInheritance.CopyFromParentOnCreate"/>：创建后独立，直接读取子控件值。</item>
    /// <item><see cref="FrontedV3PropertyInheritance.LockedToParent"/>：始终返回父控件的值（当父控件或父属性缺失时回退到子控件值）。</item>
    /// </list>
    /// </para>
    /// </remarks>
    public object? ReadValueWithInheritance(
        FrontedV3PropertyDefinition property,
        FrontedControlConfigBase childConfig,
        FrontedControlConfigBase? parentConfig,
        FrontedV3PropertyDefinition? parentProperty)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(childConfig);

        var inheritance = property.Metadata.Inheritance;

        switch (inheritance)
        {
            case FrontedV3PropertyInheritance.LockedToParent:
                // 锁定到父值：始终返回父控件的值；父缺失时回退到子值。
                if (parentConfig is not null && parentProperty is not null)
                {
                    return parentProperty.GetValue(parentConfig);
                }

                return property.GetValue(childConfig);

            case FrontedV3PropertyInheritance.ParentFallback:
                // 动态读取：子项 override 优先，没有则回退到父 OptionsPath。
                var childValue = property.GetValue(childConfig);
                if (!IsOverrideMissing(childValue))
                {
                    return childValue;
                }

                if (parentConfig is not null && parentProperty is not null)
                {
                    return parentProperty.GetValue(parentConfig);
                }

                return childValue;

            case FrontedV3PropertyInheritance.None:
            case FrontedV3PropertyInheritance.CopyFromParentOnCreate:
            default:
                return property.GetValue(childConfig);
        }
    }

    /// <summary>
    /// 尝试在子控件上写入属性 override，遵守 <see cref="FrontedV3PropertyInheritance.LockedToParent"/> 约束。
    /// </summary>
    /// <param name="property">子控件的属性定义（包含继承模式元数据）。</param>
    /// <param name="childConfig">子控件配置实例。</param>
    /// <param name="value">要写入的值。</param>
    /// <returns>当写入成功时为 <see langword="true"/>；当属性为 <see cref="FrontedV3PropertyInheritance.LockedToParent"/> 时返回 <see langword="false"/>。</returns>
    /// <remarks>
    /// <see cref="FrontedV3PropertyInheritance.LockedToParent"/> 模式拒绝 override 写入。
    /// 其他继承模式均允许写入到子控件的存储。
    /// </remarks>
    public bool TrySetChildValue(
        FrontedV3PropertyDefinition property,
        FrontedControlConfigBase childConfig,
        object? value)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(childConfig);

        if (property.Metadata.Inheritance == FrontedV3PropertyInheritance.LockedToParent)
        {
            return false;
        }

        property.SetValue(childConfig, value);
        return true;
    }

    /// <summary>
    /// 将父控件的 Appearance（或 profile 开启的语义）属性值传播给子控件，按相同 OptionsPath 匹配父子属性。
    /// </summary>
    /// <param name="parentProperties">父控件的属性定义列表。</param>
    /// <param name="parentConfig">父控件配置实例。</param>
    /// <param name="childProperties">子控件的属性定义列表。</param>
    /// <param name="childConfigs">要应用到的子控件配置列表。</param>
    /// <param name="profile">本次操作的传播 profile；为 <see langword="null"/> 时使用 <see cref="FrontedV3StyleTransferProfile.Default"/>。</param>
    /// <remarks>
    /// <para>
    /// 匹配规则：对每个子控件属性，在父属性列表中查找 OptionsPath 完全相同的父属性，
    /// 将父属性的值（通过父存储在父 Config 上读取）写入子属性（通过子存储在子 Config 上写入）。
    /// </para>
    /// <para>
    /// 传播范围：只传播同时满足以下两个条件的属性：
    /// <list type="bullet">
    /// <item>属性语义被 profile 选中（<see cref="FrontedV3StyleTransferProfile.ShouldTransfer"/>）。</item>
    /// <item>子属性不在 <see cref="FrontedV3PropertyInheritance.LockedToParent"/> 模式（该模式拒绝写入）。</item>
    /// </list>
    /// </para>
    /// <para>
    /// <see cref="FrontedV3PropertySemantic.DataIdentity"/> 和 <see cref="FrontedV3PropertySemantic.Other"/> 语义的属性不会被 profile 选中，因此永远不会传播。
    /// </para>
    /// </remarks>
    public void ApplyParentStyle(
        IReadOnlyList<FrontedV3PropertyDefinition> parentProperties,
        FrontedControlConfigBase parentConfig,
        IReadOnlyList<FrontedV3PropertyDefinition> childProperties,
        IEnumerable<FrontedControlConfigBase> childConfigs,
        FrontedV3StyleTransferProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(parentProperties);
        ArgumentNullException.ThrowIfNull(parentConfig);
        ArgumentNullException.ThrowIfNull(childProperties);
        ArgumentNullException.ThrowIfNull(childConfigs);

        profile ??= FrontedV3StyleTransferProfile.Default;

        var parentByPath = BuildOptionsPathIndex(parentProperties);
        var transferableChildProps = FilterTransferableChildProperties(childProperties, profile);

        foreach (var childConfig in childConfigs)
        {
            if (childConfig is null)
            {
                continue;
            }

            foreach (var childProp in transferableChildProps)
            {
                if (childProp.Metadata.Inheritance == FrontedV3PropertyInheritance.LockedToParent)
                {
                    continue;
                }

                if (!parentByPath.TryGetValue(childProp.OptionsPath, out var parentProp))
                {
                    continue;
                }

                var parentValue = parentProp.GetValue(parentConfig);
                childProp.SetValue(childConfig, parentValue);
            }
        }
    }

    /// <summary>
    /// 清除子控件的 override，使 <see cref="FrontedV3PropertyInheritance.ParentFallback"/> 与 <see cref="FrontedV3PropertyInheritance.LockedToParent"/> 属性恢复回退到父值。
    /// </summary>
    /// <param name="childProperties">子控件的属性定义列表。</param>
    /// <param name="childConfigs">要清除 override 的子控件配置列表。</param>
    /// <param name="profile">本次操作的传播 profile；为 <see langword="null"/> 时使用 <see cref="FrontedV3StyleTransferProfile.Default"/>。</param>
    /// <remarks>
    /// <para>
    /// 清除规则：对每个子控件属性，当满足以下条件时将值设为 <see langword="null"/>：
    /// <list type="bullet">
    /// <item>继承模式为 <see cref="FrontedV3PropertyInheritance.ParentFallback"/> 或 <see cref="FrontedV3PropertyInheritance.LockedToParent"/>。</item>
    /// <item>属性语义被 profile 选中。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 清除后，<see cref="ReadValueWithInheritance"/> 读取子控件属性时将回退到父值。
    /// 对于不可清空（非 nullable CLR 属性）的存储，清除操作为 no-op。
    /// </para>
    /// </remarks>
    public void ClearChildOverrides(
        IReadOnlyList<FrontedV3PropertyDefinition> childProperties,
        IEnumerable<FrontedControlConfigBase> childConfigs,
        FrontedV3StyleTransferProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(childProperties);
        ArgumentNullException.ThrowIfNull(childConfigs);

        profile ??= FrontedV3StyleTransferProfile.Default;

        var clearableChildProps = FilterTransferableChildProperties(childProperties, profile)
            .Where(p => p.Metadata.Inheritance == FrontedV3PropertyInheritance.ParentFallback
                        || p.Metadata.Inheritance == FrontedV3PropertyInheritance.LockedToParent)
            .ToList();

        foreach (var childConfig in childConfigs)
        {
            if (childConfig is null)
            {
                continue;
            }

            foreach (var childProp in clearableChildProps)
            {
                ClearStorageValue(childProp, childConfig);
            }
        }
    }

    /// <summary>
    /// 在同 peer 控件之间传播 Appearance（或 profile 开启的语义）属性值。
    /// </summary>
    /// <param name="sourceRegistration">源控件的注册信息。</param>
    /// <param name="sourceConfig">源控件配置实例。</param>
    /// <param name="peers">目标 peer 列表。</param>
    /// <param name="profile">本次操作的传播 profile；为 <see langword="null"/> 时使用 <see cref="FrontedV3StyleTransferProfile.Default"/>。</param>
    /// <exception cref="ArgumentException">当某个 peer 的 CanonicalControlType 与源不完全相同时抛出。</exception>
    /// <remarks>
    /// <para>
    /// <b>精确匹配约束</b>：只匹配完全相同的 <see cref="FrontedV3ControlRegistration.CanonicalControlType"/>。
    /// <c>plugin:a/TeamCard</c> 不能传播给 <c>plugin:b/TeamCard</c>。
    /// 当 peer 的 CanonicalControlType 与源不同时，抛出 <see cref="ArgumentException"/>。
    /// </para>
    /// <para>
    /// 传播范围：只传播同时满足以下两个条件的属性：
    /// <list type="bullet">
    /// <item>属性语义被 profile 选中。</item>
    /// <item>属性语义被源注册的 <see cref="FrontedV3ControlRegistration.StyleTransfer"/> 能力允许。</item>
    /// </list>
    /// <see cref="FrontedV3PropertySemantic.DataIdentity"/> 和 <see cref="FrontedV3PropertySemantic.Other"/> 永远不会被传播。
    /// </para>
    /// <para>
    /// 传播方式：源与 peer 使用相同的注册（因此有相同的属性列表与存储访问器），
    /// 通过 <see cref="FrontedV3PropertyDefinition.GetValue"/> 读取源值，再通过 <see cref="FrontedV3PropertyDefinition.SetValue"/> 写入每个 peer。
    /// </para>
    /// </remarks>
    public void TransferPeerStyle(
        FrontedV3ControlRegistration sourceRegistration,
        FrontedControlConfigBase sourceConfig,
        IReadOnlyList<PeerStyleTarget> peers,
        FrontedV3StyleTransferProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(sourceRegistration);
        ArgumentNullException.ThrowIfNull(sourceConfig);
        ArgumentNullException.ThrowIfNull(peers);

        profile ??= FrontedV3StyleTransferProfile.Default;
        var transferCapabilities = sourceRegistration.StyleTransfer ?? FrontedV3PropertyTransfer.Default;

        var transferableProps = sourceRegistration.Properties
            .Where(p => IsTransferable(p.Metadata.Semantic, transferCapabilities, profile))
            .ToList();

        foreach (var peer in peers)
        {
            if (peer is null || peer.Registration is null || peer.Config is null)
            {
                continue;
            }

            if (!string.Equals(
                peer.Registration.CanonicalControlType,
                sourceRegistration.CanonicalControlType,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Peer CanonicalControlType '{peer.Registration.CanonicalControlType}' " +
                    $"does not match source '{sourceRegistration.CanonicalControlType}'. " +
                    "Peer style transfer requires exact CanonicalControlType match.");
            }

            foreach (var prop in transferableProps)
            {
                var value = prop.GetValue(sourceConfig);
                prop.SetValue(peer.Config, value);
            }
        }
    }

    /// <summary>
    /// 判断给定属性语义是否同时被能力声明与 profile 允许传播。
    /// </summary>
    /// <param name="semantic">属性语义。</param>
    /// <param name="transfer">能力声明。</param>
    /// <param name="profile">传播 profile。</param>
    /// <returns>当能力允许且 profile 选中时为 <see langword="true"/>。</returns>
    /// <remarks>
    /// <see cref="FrontedV3PropertySemantic.DataIdentity"/> 和 <see cref="FrontedV3PropertySemantic.Other"/> 永远返回 <see langword="false"/>。
    /// </remarks>
    private static bool IsTransferable(
        FrontedV3PropertySemantic semantic,
        FrontedV3PropertyTransfer transfer,
        FrontedV3StyleTransferProfile profile)
    {
        return transfer.CanTransfer(semantic) && profile.ShouldTransfer(semantic);
    }

    /// <summary>
    /// 过滤出同时被能力声明与 profile 允许传播的子属性列表（不使用注册级能力，因为父子可能不同注册）。
    /// </summary>
    /// <param name="childProperties">子属性列表。</param>
    /// <param name="profile">传播 profile。</param>
    /// <returns>被 profile 选中的子属性列表。</returns>
    /// <remarks>
    /// <para>
    /// 对于 ApplyParentStyle / ClearChildOverrides，父子可能使用不同注册，
    /// 因此只检查 profile 选择，不检查注册级能力（注册级能力用于 PeerTransfer）。
    /// </para>
    /// <para>
    /// <see cref="FrontedV3PropertySemantic.DataIdentity"/> 和 <see cref="FrontedV3PropertySemantic.Other"/>
    /// 永远不会被 profile 选中（<see cref="FrontedV3StyleTransferProfile.ShouldTransfer"/> 返回 <see langword="false"/>）。
    /// </para>
    /// </remarks>
    private static List<FrontedV3PropertyDefinition> FilterTransferableChildProperties(
        IReadOnlyList<FrontedV3PropertyDefinition> childProperties,
        FrontedV3StyleTransferProfile profile)
    {
        var result = new List<FrontedV3PropertyDefinition>();
        foreach (var prop in childProperties)
        {
            if (profile.ShouldTransfer(prop.Metadata.Semantic))
            {
                result.Add(prop);
            }
        }

        return result;
    }

    /// <summary>
    /// 构建按 OptionsPath 索引的属性查找表。
    /// </summary>
    /// <param name="properties">属性列表。</param>
    /// <returns>OptionsPath 到属性定义的字典（序数比较）。</returns>
    private static Dictionary<string, FrontedV3PropertyDefinition> BuildOptionsPathIndex(
        IReadOnlyList<FrontedV3PropertyDefinition> properties)
    {
        var dict = new Dictionary<string, FrontedV3PropertyDefinition>(StringComparer.Ordinal);
        foreach (var prop in properties)
        {
            // 首次出现的 OptionsPath 优先；重复的 OptionsPath 在注册时已被拒绝。
            dict.TryAdd(prop.OptionsPath, prop);
        }

        return dict;
    }

    /// <summary>
    /// 判断子控件的 override 是否缺失（应回退到父值）。
    /// </summary>
    /// <param name="childValue">子控件存储的原始值。</param>
    /// <returns>当值为 <see langword="null"/> 或 <see cref="JsonElement"/> 的 <see cref="JsonValueKind.Undefined"/>/<see cref="JsonValueKind.Null"/> 时为 <see langword="true"/>。</returns>
    /// <remarks>
    /// 对于 ExtensionData 存储，缺失键返回 <see langword="null"/>；存在的 null 值返回 <see cref="JsonValueKind.Null"/> 的 <see cref="JsonElement"/>。
    /// 两种情况都视为 override 缺失，应回退到父值。
    /// </remarks>
    private static bool IsOverrideMissing(object? childValue)
    {
        if (childValue is null)
        {
            return true;
        }

        if (childValue is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Undefined
                   || element.ValueKind == JsonValueKind.Null;
        }

        return false;
    }

    /// <summary>
    /// 清除存储上的 override 值，将属性设为 <see langword="null"/>。
    /// </summary>
    /// <param name="property">要清除的属性定义。</param>
    /// <param name="config">配置实例。</param>
    /// <remarks>
    /// 对于 nullable CLR 属性与 ExtensionData 存储，写入 <see langword="null"/> 会清除 override。
    /// 对于非 nullable 值类型 CLR 属性，<see cref="FrontedV3Storage"/> 的 ClrProperty 存储会通过值转换
    /// 将 <see langword="null"/> 转为类型默认值，此时清除不会真正恢复回退（因为非 nullable 属性没有"缺失"状态）。
    /// </remarks>
    private static void ClearStorageValue(FrontedV3PropertyDefinition property, FrontedControlConfigBase config)
    {
        // 写入 null：ExtensionData 存储会设置 JsonElement Null；
        // nullable CLR 属性会设为 null；非 nullable CLR 属性会设为默认值（不会触发回退）。
        property.SetValue(config, null);
    }
}
