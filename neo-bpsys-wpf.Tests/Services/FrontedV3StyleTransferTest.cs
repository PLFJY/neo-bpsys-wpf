#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.StyleTransfer;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Phase 5 SubTask 6.6 测试：覆盖 <see cref="FrontedV3StyleTransferService"/> 的继承模式、
/// Parent Style 操作与 Peer Style Transfer 的全部场景。
/// </summary>
/// <remarks>
/// <para>
/// 这些测试验证 Phase 5 的核心契约：
/// <list type="bullet">
/// <item><see cref="FrontedV3PropertyInheritance.ParentFallback"/> 动态读取：子项 override 优先，无则回退到父 OptionsPath。</item>
/// <item><see cref="FrontedV3PropertyInheritance.LockedToParent"/> 拒绝 override 写入并始终返回父值。</item>
/// <item>ApplyParentStyle 按相同 OptionsPath 匹配父子属性并传播。</item>
/// <item>ClearChildOverrides 清除 ParentFallback 属性 override，恢复回退。</item>
/// <item>Peer Style Transfer 要求精确 CanonicalControlType 匹配。</item>
/// <item>Appearance 默认传播；DataIdentity 永不传播；RootSize/PartLayout/Behaviors 仅 profile 开启时传播。</item>
/// </list>
/// </para>
/// <para>
/// 这些是数据流契约测试（验证属性值在 Config 之间的传播），不涉及 WPF 视觉树，
/// 因此不需要 <see cref="neo_bpsys_wpf.Tests.Infrastructure.WpfTestThread"/>。
/// </para>
/// </remarks>
public class FrontedV3StyleTransferTest
{
    // -------------------------------------------------------------------
    // 1. ParentFallbackUsesParentWhenOverrideMissing
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3PropertyInheritance.ParentFallback"/> 模式下，子控件未设置 override 时，
    /// <see cref="FrontedV3StyleTransferService.ReadValueWithInheritance"/> 必须回退到父控件同 OptionsPath 的值。
    /// </summary>
    [Fact]
    public void ParentFallbackUsesParentWhenOverrideMissing()
    {
        var service = new FrontedV3StyleTransferService();
        var parentConfig = CreateConfig();
        var childConfig = CreateConfig();
        SetExtensionData(parentConfig, "TextColor", "parent-red");

        var childProp = CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance, inheritance: FrontedV3PropertyInheritance.ParentFallback);
        var parentProp = CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance, inheritance: FrontedV3PropertyInheritance.None);

        var value = service.ReadValueWithInheritance(childProp, childConfig, parentConfig, parentProp);

        Assert.Equal("parent-red", value);
    }

    // -------------------------------------------------------------------
    // 2. ParentOverrideWins
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3PropertyInheritance.ParentFallback"/> 模式下，子控件已设置 override 时，
    /// <see cref="FrontedV3StyleTransferService.ReadValueWithInheritance"/> 必须返回子控件的 override 值，不使用父值。
    /// </summary>
    [Fact]
    public void ParentOverrideWins()
    {
        var service = new FrontedV3StyleTransferService();
        var parentConfig = CreateConfig();
        var childConfig = CreateConfig();
        SetExtensionData(parentConfig, "TextColor", "parent-red");
        SetExtensionData(childConfig, "TextColor", "child-blue");

        var childProp = CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance, inheritance: FrontedV3PropertyInheritance.ParentFallback);
        var parentProp = CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance, inheritance: FrontedV3PropertyInheritance.None);

        var value = service.ReadValueWithInheritance(childProp, childConfig, parentConfig, parentProp);

        Assert.Equal("child-blue", value);
    }

    // -------------------------------------------------------------------
    // 3. LockedToParentRejectsOverride
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3PropertyInheritance.LockedToParent"/> 模式下，
    /// <see cref="FrontedV3StyleTransferService.TrySetChildValue"/> 必须拒绝写入并返回 <see langword="false"/>，
    /// <see cref="FrontedV3StyleTransferService.ReadValueWithInheritance"/> 必须始终返回父控件的值。
    /// </summary>
    [Fact]
    public void LockedToParentRejectsOverride()
    {
        var service = new FrontedV3StyleTransferService();
        var parentConfig = CreateConfig();
        var childConfig = CreateConfig();
        SetExtensionData(parentConfig, "TextColor", "parent-green");

        var childProp = CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance, inheritance: FrontedV3PropertyInheritance.LockedToParent);
        var parentProp = CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance, inheritance: FrontedV3PropertyInheritance.None);

        // 写入必须被拒绝。
        var setResult = service.TrySetChildValue(childProp, childConfig, "child-override");
        Assert.False(setResult);

        // 读取必须返回父值。
        var value = service.ReadValueWithInheritance(childProp, childConfig, parentConfig, parentProp);
        Assert.Equal("parent-green", value);
    }

    // -------------------------------------------------------------------
    // 4. ApplyParentStyleUsesMatchingOptionsPath
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3StyleTransferService.ApplyParentStyle"/> 必须按相同 OptionsPath 匹配父子属性，
    /// 将父控件 Appearance 属性值传播给子控件。
    /// </summary>
    [Fact]
    public void ApplyParentStyleUsesMatchingOptionsPath()
    {
        var service = new FrontedV3StyleTransferService();
        var parentConfig = CreateConfig();
        var childConfig1 = CreateConfig();
        var childConfig2 = CreateConfig();
        SetExtensionData(parentConfig, "TextColor", "parent-red");
        SetExtensionData(parentConfig, "FontSize", 18);

        var parentProps = new List<FrontedV3PropertyDefinition>
        {
            CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance),
            CreateProperty("Appearance.FontSize", "FontSize", semantic: FrontedV3PropertySemantic.Appearance, propertyType: typeof(int))
        };
        var childProps = new List<FrontedV3PropertyDefinition>
        {
            CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance, inheritance: FrontedV3PropertyInheritance.ParentFallback),
            CreateProperty("Appearance.FontSize", "FontSize", semantic: FrontedV3PropertySemantic.Appearance, inheritance: FrontedV3PropertyInheritance.ParentFallback, propertyType: typeof(int))
        };

        service.ApplyParentStyle(parentProps, parentConfig, childProps, new[] { childConfig1, childConfig2 });

        Assert.Equal("parent-red", GetExtensionData<string>(childConfig1, "TextColor"));
        Assert.Equal("parent-red", GetExtensionData<string>(childConfig2, "TextColor"));
        Assert.Equal(18, GetExtensionData<int>(childConfig1, "FontSize"));
        Assert.Equal(18, GetExtensionData<int>(childConfig2, "FontSize"));
    }

    // -------------------------------------------------------------------
    // 5. ClearOverridesRestoresFallback
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3StyleTransferService.ClearChildOverrides"/> 必须清除 <see cref="FrontedV3PropertyInheritance.ParentFallback"/>
    /// 属性的 override，使后续 <see cref="FrontedV3StyleTransferService.ReadValueWithInheritance"/> 回退到父值。
    /// </summary>
    [Fact]
    public void ClearOverridesRestoresFallback()
    {
        var service = new FrontedV3StyleTransferService();
        var parentConfig = CreateConfig();
        var childConfig = CreateConfig();
        SetExtensionData(parentConfig, "TextColor", "parent-red");
        SetExtensionData(childConfig, "TextColor", "child-blue");

        var childProp = CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance, inheritance: FrontedV3PropertyInheritance.ParentFallback);
        var parentProp = CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance, inheritance: FrontedV3PropertyInheritance.None);
        var childProps = new List<FrontedV3PropertyDefinition> { childProp };

        // 清除前：子 override 生效。
        Assert.Equal("child-blue", service.ReadValueWithInheritance(childProp, childConfig, parentConfig, parentProp));

        service.ClearChildOverrides(childProps, new[] { childConfig });

        // 清除后：回退到父值。
        Assert.Equal("parent-red", service.ReadValueWithInheritance(childProp, childConfig, parentConfig, parentProp));
    }

    // -------------------------------------------------------------------
    // 6. PeerTransferRequiresExactCanonicalType
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3StyleTransferService.TransferPeerStyle"/> 必须要求 peer 的 CanonicalControlType 与源完全相同；
    /// 不同时必须抛出 <see cref="ArgumentException"/>。
    /// </summary>
    [Fact]
    public void PeerTransferRequiresExactCanonicalType()
    {
        var service = new FrontedV3StyleTransferService();
        var sourceConfig = CreateConfig("plugin:a/TeamCard");
        SetExtensionData(sourceConfig, "TextColor", "source-red");

        var sourceProps = new List<FrontedV3PropertyDefinition>
        {
            CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance)
        };
        var sourceRegistration = CreateRegistration("plugin:a/TeamCard", sourceProps);

        var peerConfig = CreateConfig("plugin:b/TeamCard");
        var peerRegistration = CreateRegistration("plugin:b/TeamCard", sourceProps);
        var peers = new List<PeerStyleTarget> { new(peerRegistration, peerConfig) };

        Assert.Throws<ArgumentException>(() =>
            service.TransferPeerStyle(sourceRegistration, sourceConfig, peers));

        // peer 未被修改。
        Assert.False(peerConfig.ExtensionData.ContainsKey("TextColor"));
    }

    // -------------------------------------------------------------------
    // 7. AppearanceTransfersByDefault
    // -------------------------------------------------------------------

    /// <summary>
    /// Peer Style Transfer 默认 profile 必须传播 <see cref="FrontedV3PropertySemantic.Appearance"/> 语义的属性。
    /// </summary>
    [Fact]
    public void AppearanceTransfersByDefault()
    {
        var service = new FrontedV3StyleTransferService();
        var sourceConfig = CreateConfig("plugin:a/TeamCard");
        SetExtensionData(sourceConfig, "TextColor", "source-red");
        SetExtensionData(sourceConfig, "FontSize", 20);

        var props = new List<FrontedV3PropertyDefinition>
        {
            CreateProperty("Appearance.TextColor", "TextColor", semantic: FrontedV3PropertySemantic.Appearance),
            CreateProperty("Appearance.FontSize", "FontSize", semantic: FrontedV3PropertySemantic.Appearance, propertyType: typeof(int))
        };
        var registration = CreateRegistration("plugin:a/TeamCard", props);

        var peerConfig = CreateConfig("plugin:a/TeamCard");
        var peers = new List<PeerStyleTarget> { new(registration, peerConfig) };

        service.TransferPeerStyle(registration, sourceConfig, peers);

        Assert.Equal("source-red", GetExtensionData<string>(peerConfig, "TextColor"));
        Assert.Equal(20, GetExtensionData<int>(peerConfig, "FontSize"));
    }

    // -------------------------------------------------------------------
    // 8. DataSemanticDoesNotTransfer
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3PropertySemantic.DataIdentity"/> 语义的属性永远不参与 Peer Style Transfer，
    /// 即使使用 <see cref="FrontedV3StyleTransferProfile.TransferAll"/> profile 也不传播。
    /// </summary>
    [Fact]
    public void DataSemanticDoesNotTransfer()
    {
        var service = new FrontedV3StyleTransferService();
        var sourceConfig = CreateConfig("plugin:a/TeamCard");
        SetExtensionData(sourceConfig, "MapKey", "source-map-key");

        var props = new List<FrontedV3PropertyDefinition>
        {
            CreateProperty("Identity.MapKey", "MapKey", semantic: FrontedV3PropertySemantic.DataIdentity)
        };
        // 即使能力声明允许所有语义，DataIdentity 仍然不传播。
        var registration = CreateRegistration(
            "plugin:a/TeamCard",
            props,
            styleTransfer: FrontedV3PropertyTransfer.All);

        var peerConfig = CreateConfig("plugin:a/TeamCard");
        var peers = new List<PeerStyleTarget> { new(registration, peerConfig) };

        service.TransferPeerStyle(registration, sourceConfig, peers, FrontedV3StyleTransferProfile.TransferAll());

        // peer 未被写入 MapKey。
        Assert.False(peerConfig.ExtensionData.ContainsKey("MapKey"));
    }

    // -------------------------------------------------------------------
    // 9. RootSizeTransfersOnlyWhenEnabled
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3PropertySemantic.RootSize"/> 语义的属性仅在 profile 显式开启时才传播；
    /// 默认 profile 不传播 RootSize。
    /// </summary>
    [Fact]
    public void RootSizeTransfersOnlyWhenEnabled()
    {
        var service = new FrontedV3StyleTransferService();
        var sourceConfig = CreateConfig("plugin:a/TeamCard");
        SetExtensionData(sourceConfig, "RootWidth", 300.0);

        var props = new List<FrontedV3PropertyDefinition>
        {
            CreateProperty("Root.Width", "RootWidth", semantic: FrontedV3PropertySemantic.RootSize, propertyType: typeof(double))
        };
        var registration = CreateRegistration(
            "plugin:a/TeamCard",
            props,
            styleTransfer: new FrontedV3PropertyTransfer { CanTransferRootSize = true });

        // 默认 profile：不传播 RootSize。
        var peerConfig1 = CreateConfig("plugin:a/TeamCard");
        service.TransferPeerStyle(
            registration,
            sourceConfig,
            new List<PeerStyleTarget> { new(registration, peerConfig1) });
        Assert.False(peerConfig1.ExtensionData.ContainsKey("RootWidth"));

        // 开启 RootSize 的 profile：传播 RootSize。
        var peerConfig2 = CreateConfig("plugin:a/TeamCard");
        service.TransferPeerStyle(
            registration,
            sourceConfig,
            new List<PeerStyleTarget> { new(registration, peerConfig2) },
            new FrontedV3StyleTransferProfile { TransferRootSize = true });
        Assert.Equal(300.0, GetExtensionData<double>(peerConfig2, "RootWidth"));
    }

    // -------------------------------------------------------------------
    // 10. PartLayoutTransfersOnlyWhenEnabled
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3PropertySemantic.PartLayout"/> 语义的属性仅在 profile 显式开启时才传播；
    /// 默认 profile 不传播 PartLayout。
    /// </summary>
    [Fact]
    public void PartLayoutTransfersOnlyWhenEnabled()
    {
        var service = new FrontedV3StyleTransferService();
        var sourceConfig = CreateConfig("plugin:a/TeamCard");
        SetExtensionData(sourceConfig, "PartX", 120.0);

        var props = new List<FrontedV3PropertyDefinition>
        {
            CreateProperty("Part.X", "PartX", semantic: FrontedV3PropertySemantic.PartLayout, propertyType: typeof(double))
        };
        var registration = CreateRegistration(
            "plugin:a/TeamCard",
            props,
            styleTransfer: new FrontedV3PropertyTransfer { CanTransferPartLayout = true });

        // 默认 profile：不传播 PartLayout。
        var peerConfig1 = CreateConfig("plugin:a/TeamCard");
        service.TransferPeerStyle(
            registration,
            sourceConfig,
            new List<PeerStyleTarget> { new(registration, peerConfig1) });
        Assert.False(peerConfig1.ExtensionData.ContainsKey("PartX"));

        // 开启 PartLayout 的 profile：传播 PartLayout。
        var peerConfig2 = CreateConfig("plugin:a/TeamCard");
        service.TransferPeerStyle(
            registration,
            sourceConfig,
            new List<PeerStyleTarget> { new(registration, peerConfig2) },
            new FrontedV3StyleTransferProfile { TransferPartLayout = true });
        Assert.Equal(120.0, GetExtensionData<double>(peerConfig2, "PartX"));
    }

    // -------------------------------------------------------------------
    // 11. BehaviorTransfersOnlyWhenEnabled
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3PropertySemantic.Behaviors"/> 语义的属性仅在 profile 显式开启时才传播；
    /// 默认 profile 不传播 Behaviors。
    /// </summary>
    [Fact]
    public void BehaviorTransfersOnlyWhenEnabled()
    {
        var service = new FrontedV3StyleTransferService();
        var sourceConfig = CreateConfig("plugin:a/TeamCard");
        var behaviorGuid = Guid.NewGuid();
        SetExtensionData(sourceConfig, "BehaviorGuid", behaviorGuid);

        var props = new List<FrontedV3PropertyDefinition>
        {
            CreateProperty("Behavior.Guid", "BehaviorGuid", semantic: FrontedV3PropertySemantic.Behaviors, propertyType: typeof(Guid))
        };
        var registration = CreateRegistration(
            "plugin:a/TeamCard",
            props,
            styleTransfer: new FrontedV3PropertyTransfer { CanTransferBehaviors = true });

        // 默认 profile：不传播 Behaviors。
        var peerConfig1 = CreateConfig("plugin:a/TeamCard");
        service.TransferPeerStyle(
            registration,
            sourceConfig,
            new List<PeerStyleTarget> { new(registration, peerConfig1) });
        Assert.False(peerConfig1.ExtensionData.ContainsKey("BehaviorGuid"));

        // 开启 Behaviors 的 profile：传播 Behaviors。
        var peerConfig2 = CreateConfig("plugin:a/TeamCard");
        service.TransferPeerStyle(
            registration,
            sourceConfig,
            new List<PeerStyleTarget> { new(registration, peerConfig2) },
            new FrontedV3StyleTransferProfile { TransferBehaviors = true });
        Assert.Equal(behaviorGuid, GetExtensionData<Guid>(peerConfig2, "BehaviorGuid"));
    }

    // -------------------------------------------------------------------
    // 12. BuiltInControl_AppearanceSemanticTransfers
    // -------------------------------------------------------------------

    /// <summary>
    /// 内置控件（以 MapV2Display 为例）的属性通过 <see cref="BuiltInPropertyDefinitionResolver"/> 解析后，
    /// 颜色等 Appearance 语义属性必须能在同类型 peer 间通过
    /// <see cref="FrontedV3StyleTransferService.TransferPeerStyle"/> 传播；
    /// 而 <see cref="FrontedV3PropertySemantic.DataIdentity"/> 语义的 <c>MapKey</c> 不得传播。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该测试验证 <see cref="BuiltInPropertyDefinitionResolver"/> 的 <c>ResolveSemantic</c> 推断与
    /// 默认 <see cref="FrontedV3PropertyTransfer"/> 能力声明（<c>CanTransferAppearance = true</c>）协同工作，
    /// 使内置控件的同类型样式传播无需额外注册即可生效。
    /// </para>
    /// <para>
    /// 属性列表通过 <see cref="BuiltInPropertyDefinitionResolver.GetProperties"/> 获取，而非手动构造，
    /// 以验证真实内置控件的 Schema 驱动路径。
    /// </para>
    /// </remarks>
    [Fact]
    public void BuiltInControl_AppearanceSemanticTransfers()
    {
        var sourceConfig = new MapV2DisplayControlConfig
        {
            ControlType = "MapV2Display",
            MapKey = "ArmsFactory",
            MapNameColor = "#FF0000",
            TeamNameColor = "#00FF00",
            CampNameColor = "#0000FF"
        };
        var peerConfig = new MapV2DisplayControlConfig
        {
            ControlType = "MapV2Display",
            MapKey = "AnotherMap"
        };

        // 通过 BuiltInPropertyDefinitionResolver 获取属性列表，而非手动构造
        var properties = BuiltInPropertyDefinitionResolver.GetProperties(sourceConfig);

        var registration = new FrontedV3ControlRegistration
        {
            CanonicalControlType = "MapV2Display",
            LocalControlId = "MapV2Display",
            PackageId = null,
            IsBuiltIn = true,
            ControlType = typeof(Border),
            ConfigType = typeof(MapV2DisplayControlConfig),
            Properties = properties,
            CreateDefaultConfig = () => new MapV2DisplayControlConfig { ControlType = "MapV2Display" }
            // StyleTransfer 使用默认值（CanTransferAppearance = true）
        };

        var service = new FrontedV3StyleTransferService();
        var peers = new List<PeerStyleTarget> { new(registration, peerConfig) };

        service.TransferPeerStyle(registration, sourceConfig, peers);

        // Appearance 语义的颜色属性应当被传播
        Assert.Equal("#FF0000", peerConfig.MapNameColor);
        Assert.Equal("#00FF00", peerConfig.TeamNameColor);
        Assert.Equal("#0000FF", peerConfig.CampNameColor);

        // DataIdentity 语义的 MapKey 不得传播
        Assert.Equal("AnotherMap", peerConfig.MapKey);
    }

    // -------------------------------------------------------------------
    // 辅助方法
    // -------------------------------------------------------------------

    private static PluginFrontedControlConfig CreateConfig(string controlType = "plugin:test/TestControl")
    {
        return new PluginFrontedControlConfig { ControlType = controlType };
    }

    private static FrontedV3PropertyDefinition CreateProperty(
        string optionsPath,
        string storageKey,
        FrontedV3PropertySemantic semantic = FrontedV3PropertySemantic.Other,
        FrontedV3PropertyInheritance inheritance = FrontedV3PropertyInheritance.None,
        Type? propertyType = null)
    {
        var metadata = new FrontedV3PropertyMetadata
        {
            Semantic = semantic,
            Inheritance = inheritance
        };
        return new FrontedV3PropertyDefinition(
            optionsPath,
            FrontedV3Storage.ExtensionData(storageKey),
            propertyType ?? typeof(string),
            metadata);
    }

    private static FrontedV3ControlRegistration CreateRegistration(
        string canonicalControlType,
        IReadOnlyList<FrontedV3PropertyDefinition> properties,
        FrontedV3PropertyTransfer? styleTransfer = null)
    {
        var localId = canonicalControlType.Contains('/')
            ? canonicalControlType[(canonicalControlType.LastIndexOf('/') + 1)..]
            : canonicalControlType;

        return new FrontedV3ControlRegistration
        {
            CanonicalControlType = canonicalControlType,
            LocalControlId = localId,
            PackageId = canonicalControlType.StartsWith("plugin:", StringComparison.Ordinal)
                ? canonicalControlType["plugin:".Length..canonicalControlType.LastIndexOf('/')]
                : null,
            IsBuiltIn = !canonicalControlType.StartsWith("plugin:", StringComparison.Ordinal),
            ControlType = typeof(Border),
            ConfigType = typeof(PluginFrontedControlConfig),
            Properties = properties,
            CreateDefaultConfig = () => new PluginFrontedControlConfig { ControlType = canonicalControlType },
            StyleTransfer = styleTransfer ?? FrontedV3PropertyTransfer.Default
        };
    }

    private static void SetExtensionData<T>(PluginFrontedControlConfig config, string key, T value)
    {
        config.ExtensionData[key] = JsonSerializer.SerializeToElement(value);
    }

    private static T? GetExtensionData<T>(PluginFrontedControlConfig config, string key)
    {
        if (!config.ExtensionData.TryGetValue(key, out var element))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(element);
    }
}
