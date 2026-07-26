#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Options;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.PluginSdk;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

/// <summary>
/// Phase 1 SubTask 2.8 测试：覆盖 v3 控件注册、属性 Schema、Options 动态代理、Storage 读写与 JSON 契约的全部场景。
/// </summary>
public class FrontedV3ControlRegistrationTest
{
    // -------------------------------------------------------------------
    // 1. MissingAttributeFails
    // -------------------------------------------------------------------

    /// <summary>
    /// 缺少 <see cref="FrontedV3ControlAttribute"/> 的控件类型在注册时必须抛出
    /// <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Fact]
    public void MissingAttributeFails()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => services.AddFrontedV3Control<ControlWithoutAttribute>());

        Assert.Contains("FrontedV3Control", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 2. DuplicateCanonicalTypeFails
    // -------------------------------------------------------------------

    /// <summary>
    /// 两个产生相同 Canonical Control Type 的注册在 Registry 构造时必须抛出
    /// <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Fact]
    public void DuplicateCanonicalTypeFails()
    {
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope("dup.pkg"))
        {
            services.AddFrontedV3Control<DuplicateControlA>();
            services.AddFrontedV3Control<DuplicateControlB>();
        }

        var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IEnumerable<FrontedV3ControlRegistration>>().ToList();

        Assert.Equal(2, registrations.Count);
        Assert.Equal(
            registrations[0].CanonicalControlType,
            registrations[1].CanonicalControlType);

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => new FrontedV3ControlRegistry(registrations));

        Assert.Contains("Duplicate", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 3. PackageIdNamespacesPluginControl
    // -------------------------------------------------------------------

    /// <summary>
    /// 插件控件 Canonical Control Type 必须为 <c>plugin:{PackageId}/{ControlId}</c>；
    /// 内置控件 Canonical Control Type 直接使用 <c>ControlId</c>。
    /// </summary>
    [Fact]
    public void PackageIdNamespacesPluginControl()
    {
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope("plfjy.ExamplePlugin"))
        {
            services.AddFrontedV3Control<V3TestTeamCardControl>();
        }

        var provider = services.BuildServiceProvider();
        var registration = provider.GetRequiredService<FrontedV3ControlRegistration>();

        Assert.Equal("plugin:plfjy.ExamplePlugin/TeamCard", registration.CanonicalControlType);
        Assert.Equal("TeamCard", registration.LocalControlId);
        Assert.Equal("plfjy.ExamplePlugin", registration.PackageId);
        Assert.False(registration.IsBuiltIn);
    }

    /// <summary>
    /// 内置控件在无插件作用域时 Canonical Control Type 直接使用 ControlId，且 IsBuiltIn 为 true。
    /// </summary>
    [Fact]
    public void BuiltInControlUsesBareLocalIdAsCanonicalType()
    {
        var services = new ServiceCollection();

        // 无插件作用域，宿主直接注册内置控件。
        Assert.Null(FrontedPluginRegistrationContext.CurrentPackageId);
        services.AddFrontedV3Control<BuiltInTextControl>();

        var provider = services.BuildServiceProvider();
        var registration = provider.GetRequiredService<FrontedV3ControlRegistration>();

        Assert.Equal("Text", registration.CanonicalControlType);
        Assert.True(registration.IsBuiltIn);
        Assert.Null(registration.PackageId);
    }

    // -------------------------------------------------------------------
    // 4. SameLocalIdAcrossPluginsSucceeds
    // -------------------------------------------------------------------

    /// <summary>
    /// 不同插件可以复用相同的 local Control ID，注册后 Canonical Control Type 不同、互不干扰。
    /// </summary>
    [Fact]
    public void SameLocalIdAcrossPluginsSucceeds()
    {
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope("plugin.a"))
        {
            services.AddFrontedV3Control<V3TestTeamCardControl>();
        }

        using (FrontedPluginRegistrationContext.BeginScope("plugin.b"))
        {
            services.AddFrontedV3Control<V3TestTeamCardControl>();
        }

        var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IEnumerable<FrontedV3ControlRegistration>>().ToList();

        Assert.Equal(2, registrations.Count);
        var canonicalTypes = registrations.Select(r => r.CanonicalControlType).ToList();
        Assert.Contains("plugin:plugin.a/TeamCard", canonicalTypes);
        Assert.Contains("plugin:plugin.b/TeamCard", canonicalTypes);

        // Registry 能正常构造，因为 Canonical Control Type 不重复。
        var registry = new FrontedV3ControlRegistry(registrations);
        Assert.NotNull(registry.GetRegistration("plugin:plugin.a/TeamCard"));
        Assert.NotNull(registry.GetRegistration("plugin:plugin.b/TeamCard"));
    }

    // -------------------------------------------------------------------
    // 5. UnsafeIdFails
    // -------------------------------------------------------------------

    /// <summary>
    /// 不安全的 Control ID（含路径分隔符、冒号、完整 canonical ID、空白等）在注册时必须被拒绝。
    /// </summary>
    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("plugin:pkg/ctrl")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \n ")]
    public void UnsafeIdFails(string unsafeId)
    {
        // 验证 validator 层面拒绝。
        Assert.False(FrontedV3ControlIdValidator.IsValidControlId(unsafeId));
        Assert.Throws<FrontedLayoutConfigException>(
            () => FrontedV3ControlIdValidator.EnsureValidControlId(unsafeId));
    }

    /// <summary>
    /// 注册入口 <see cref="FrontedV3ControlRegistryExtensions.AddFrontedV3Control{TControl}"/>
    /// 也必须拒绝不安全的 Control ID。
    /// </summary>
    [Fact]
    public void RegistrationRejectsUnsafeControlId()
    {
        var services = new ServiceCollection();

        Assert.Throws<FrontedLayoutConfigException>(
            () => services.AddFrontedV3Control<ControlWithFullCanonicalId>());
    }

    // -------------------------------------------------------------------
    // 6. OptionsPathMustBeUnique
    // -------------------------------------------------------------------

    /// <summary>
    /// 同一控件内两条属性使用相同 OptionsPath 时，注册必须抛出 <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Fact]
    public void OptionsPathMustBeUnique()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => services.AddFrontedV3Control<ControlWithDuplicateOptionsPath>());

        Assert.Contains("duplicate OptionsPath", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Appearance.TextColor", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 7. ReservedStorageFieldFails
    // -------------------------------------------------------------------

    /// <summary>
    /// 所有根级保留字段必须被 <see cref="FrontedV3ReservedFields"/> 正确识别。
    /// </summary>
    [Theory]
    [InlineData("Left")]
    [InlineData("Top")]
    [InlineData("Width")]
    [InlineData("Height")]
    [InlineData("ZIndex")]
    [InlineData("Visibility")]
    [InlineData("BehaviorGuid")]
    [InlineData("GaussianBlur")]
    [InlineData("ControlType")]
    public void ReservedStorageFieldFails(string reservedField)
    {
        Assert.True(FrontedV3ReservedFields.IsReserved(reservedField));
        // 大小写不敏感
        Assert.True(FrontedV3ReservedFields.IsReserved(reservedField.ToLowerInvariant()));
        Assert.True(FrontedV3ReservedFields.IsReserved(reservedField.ToUpperInvariant()));
    }

    /// <summary>
    /// 非保留字段不应被误判。
    /// </summary>
    [Fact]
    public void NonReservedFieldsAreNotReserved()
    {
        Assert.False(FrontedV3ReservedFields.IsReserved("TextColor"));
        Assert.False(FrontedV3ReservedFields.IsReserved("TeamName"));
        Assert.False(FrontedV3ReservedFields.IsReserved(""));
        Assert.False(FrontedV3ReservedFields.IsReserved(null!));
    }

    /// <summary>
    /// 注册时属性的 Storage TargetField 指向保留字段必须抛出 <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Fact]
    public void RegistrationRejectsReservedStorageField()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => services.AddFrontedV3Control<ControlWithReservedStorage>());

        Assert.Contains("reserved storage field", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Left", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 8. OptionsReadUsesCurrentConfig
    // -------------------------------------------------------------------

    /// <summary>
    /// Options 视图读取属性时必须调用 Storage 读取当前 Config 值，不缓存独立值。
    /// </summary>
    [Fact]
    public void OptionsReadUsesCurrentConfig()
    {
        var config = new PluginFrontedControlConfig { ControlType = "plugin:a/TeamCard" };
        config.ExtensionData["TextColor"] = JsonSerializer.SerializeToElement("red");

        var properties = new List<FrontedV3PropertyDefinition>
        {
            new("Appearance.TextColor", FrontedV3Storage.ExtensionData("TextColor"), typeof(string), new FrontedV3PropertyMetadata()),
        };
        var view = FrontedV3OptionsView.Create(config, properties);

        var appearanceDescriptor = TypeDescriptor.GetProperties(view)["Appearance"];
        Assert.NotNull(appearanceDescriptor);
        var subView = (FrontedV3OptionsView)appearanceDescriptor!.GetValue(view)!;

        var textColorDescriptor = TypeDescriptor.GetProperties(subView)["TextColor"];
        Assert.NotNull(textColorDescriptor);

        // 初次读取应返回当前 Config 中的值。
        Assert.Equal("red", textColorDescriptor!.GetValue(subView));

        // 直接修改 Config 后再读取，应返回新值——证明 Options 不缓存。
        config.ExtensionData["TextColor"] = JsonSerializer.SerializeToElement("blue");
        Assert.Equal("blue", textColorDescriptor.GetValue(subView));
    }

    // -------------------------------------------------------------------
    // 9. OptionsWriteUpdatesConfigImmediately
    // -------------------------------------------------------------------

    /// <summary>
    /// Options 视图修改属性时必须立即写回 Config 并触发 <see cref="INotifyPropertyChanged.PropertyChanged"/>。
    /// </summary>
    [Fact]
    public void OptionsWriteUpdatesConfigImmediately()
    {
        var config = new PluginFrontedControlConfig { ControlType = "plugin:a/TeamCard" };
        var properties = new List<FrontedV3PropertyDefinition>
        {
            new("Appearance.TextColor", FrontedV3Storage.ExtensionData("TextColor"), typeof(string), new FrontedV3PropertyMetadata()),
        };
        var view = FrontedV3OptionsView.Create(config, properties);

        var appearanceDescriptor = TypeDescriptor.GetProperties(view)["Appearance"];
        var subView = (FrontedV3OptionsView)appearanceDescriptor!.GetValue(view)!;
        var textColorDescriptor = TypeDescriptor.GetProperties(subView)["TextColor"];

        string? changedName = null;
        subView.PropertyChanged += (_, e) => changedName = e.PropertyName;

        textColorDescriptor!.SetValue(subView, "green");

        // Config 立即更新。
        Assert.True(config.ExtensionData.TryGetValue("TextColor", out var element));
        Assert.Equal("green", element.GetString());

        // PropertyChanged 触发。
        Assert.Equal("TextColor", changedName);
    }

    // -------------------------------------------------------------------
    // 10. SerializedJsonHasNoOptionsObject
    // -------------------------------------------------------------------

    /// <summary>
    /// 序列化插件控件 Config 时，JSON 根级直接包含属性字段，不得出现 "Options" 嵌套对象。
    /// </summary>
    [Fact]
    public void SerializedJsonHasNoOptionsObject()
    {
        var config = new PluginFrontedControlConfig { ControlType = "plugin:a/TeamCard" };
        config.ExtensionData["TextColor"] = JsonSerializer.SerializeToElement("#FFFFFFFF");
        config.ExtensionData["TeamName"] = JsonSerializer.SerializeToElement("ASG");

        var json = JsonSerializer.Serialize(config);

        // 根级字段存在。
        Assert.Contains("\"TextColor\"", json, StringComparison.Ordinal);
        Assert.Contains("\"TeamName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ControlType\"", json, StringComparison.Ordinal);

        // 不出现 Options 嵌套对象。
        Assert.DoesNotContain("\"Options\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Appearance\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Content\"", json, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 11. ExtensionDataFieldsRoundTrip
    // -------------------------------------------------------------------

    /// <summary>
    /// ExtensionData 中的字段在 JSON 序列化-反序列化往返后必须保留，且类型可还原。
    /// </summary>
    [Fact]
    public void ExtensionDataFieldsRoundTrip()
    {
        var config = new PluginFrontedControlConfig { ControlType = "plugin:a/TeamCard" };
        config.ExtensionData["TextColor"] = JsonSerializer.SerializeToElement("#FFFFFFFF");
        config.ExtensionData["TeamName"] = JsonSerializer.SerializeToElement("ASG");
        config.ExtensionData["FontSize"] = JsonSerializer.SerializeToElement(24);
        config.ExtensionData["IsBold"] = JsonSerializer.SerializeToElement(true);

        var json = JsonSerializer.Serialize(config);
        var deserialized = JsonSerializer.Deserialize<PluginFrontedControlConfig>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("plugin:a/TeamCard", deserialized!.ControlType);
        Assert.Equal("#FFFFFFFF", deserialized.ExtensionData["TextColor"].GetString());
        Assert.Equal("ASG", deserialized.ExtensionData["TeamName"].GetString());
        Assert.Equal(24, deserialized.ExtensionData["FontSize"].GetInt32());
        Assert.True(deserialized.ExtensionData["IsBold"].GetBoolean());
    }

    // -------------------------------------------------------------------
    // 额外：插件不能伪装内置控件
    // -------------------------------------------------------------------

    /// <summary>
    /// 插件在插件作用域内设置 <c>IsBuiltIn=true</c> 必须被拒绝，只有宿主能注册内置控件。
    /// </summary>
    [Fact]
    public void PluginCannotRegisterAsBuiltIn()
    {
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope("evil.plugin"))
        {
            var ex = Assert.Throws<FrontedLayoutConfigException>(
                () => services.AddFrontedV3Control<PluginControlClaimingBuiltIn>());

            Assert.Contains("built-in", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    // -------------------------------------------------------------------
    // 额外：禁止 OptionsPath 被拒绝
    // -------------------------------------------------------------------

    /// <summary>
    /// OptionsPath 使用 <c>Options.</c> 前缀（如 <c>Options.Layout</c>）必须被拒绝。
    /// </summary>
    [Fact]
    public void ForbiddenOptionsPathFails()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => services.AddFrontedV3Control<ControlWithForbiddenOptionsPath>());

        Assert.Contains("forbidden", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------
    // 额外：注册后 Registry 能按 Canonical Type 查找
    // -------------------------------------------------------------------

    /// <summary>
    /// 注册后 <see cref="IFrontedV3ControlRegistry"/> 能按 Canonical Control Type 查找注册信息。
    /// </summary>
    [Fact]
    public void RegistryFindsRegisteredControlByCanonicalType()
    {
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope("lookup.pkg"))
        {
            services.AddFrontedV3Control<V3TestTeamCardControl>();
        }

        services.AddSingleton<IFrontedV3ControlRegistry, FrontedV3ControlRegistry>();
        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IFrontedV3ControlRegistry>();

        Assert.True(registry.TryGetRegistration("plugin:lookup.pkg/TeamCard", out var registration));
        Assert.NotNull(registration);
        Assert.Equal("TeamCard", registration!.LocalControlId);
        Assert.Equal("lookup.pkg", registration.PackageId);

        Assert.Null(registry.GetRegistration("plugin:nonexistent/Unknown"));
    }

    // -------------------------------------------------------------------
    // 额外：属性定义通过反射发现
    // -------------------------------------------------------------------

    /// <summary>
    /// 控件类上的 <c>public static readonly FrontedV3Property&lt;T&gt;</c> 字段在注册时被自动发现并转为定义。
    /// </summary>
    [Fact]
    public void PropertiesAreDiscoveredViaReflection()
    {
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope("reflect.pkg"))
        {
            services.AddFrontedV3Control<V3TestTeamCardControl>();
        }

        var provider = services.BuildServiceProvider();
        var registration = provider.GetRequiredService<FrontedV3ControlRegistration>();

        Assert.Equal(2, registration.Properties.Count);
        var paths = registration.Properties.Select(p => p.OptionsPath).ToList();
        Assert.Contains("Appearance.TextColor", paths);
        Assert.Contains("Content.TeamName", paths);

        var textColorProp = registration.Properties.First(p => p.OptionsPath == "Appearance.TextColor");
        Assert.Equal(typeof(string), textColorProp.PropertyType);
        Assert.Equal("TextColor", textColorProp.Storage.TargetField);

        var teamNameProp = registration.Properties.First(p => p.OptionsPath == "Content.TeamName");
        Assert.Equal(typeof(string), teamNameProp.PropertyType);
        Assert.Equal("TeamName", teamNameProp.Storage.TargetField);
    }
}

// ===========================================================================
// 测试用控件类型
// ===========================================================================

/// <summary>
/// 缺少 <see cref="FrontedV3ControlAttribute"/> 的控件类型，用于测试注册拒绝。
/// </summary>
public sealed class ControlWithoutAttribute : FrontedV3ControlBase
{
}

/// <summary>
/// 正常的插件控件，用于测试属性发现与注册流程。
/// </summary>
[FrontedV3Control("TeamCard")]
public sealed class V3TestTeamCardControl : FrontedV3ControlBase
{
    /// <summary>
    /// 文本颜色属性。
    /// </summary>
    public static readonly FrontedV3Property<string> TextColorProperty =
        new("Appearance.TextColor", FrontedV3Storage.ExtensionData("TextColor"));

    /// <summary>
    /// 队伍名称属性。
    /// </summary>
    public static readonly FrontedV3Property<string> TeamNameProperty =
        new("Content.TeamName", FrontedV3Storage.ExtensionData("TeamName"));
}

/// <summary>
/// 内置控件，IsBuiltIn = true。
/// </summary>
[FrontedV3Control("Text", IsBuiltIn = true)]
public sealed class BuiltInTextControl : FrontedV3ControlBase
{
}

/// <summary>
/// 与 <see cref="DuplicateControlB"/> 使用相同 ControlId，用于测试重复 Canonical Type 检测。
/// </summary>
[FrontedV3Control("Duplicate")]
public sealed class DuplicateControlA : FrontedV3ControlBase
{
}

/// <summary>
/// 与 <see cref="DuplicateControlA"/> 使用相同 ControlId，用于测试重复 Canonical Type 检测。
/// </summary>
[FrontedV3Control("Duplicate")]
public sealed class DuplicateControlB : FrontedV3ControlBase
{
}

/// <summary>
/// 包含两条 OptionsPath 相同的属性，用于测试唯一性校验。
/// </summary>
[FrontedV3Control("DuplicatePath")]
public sealed class ControlWithDuplicateOptionsPath : FrontedV3ControlBase
{
    /// <summary>
    /// 第一条属性。
    /// </summary>
    public static readonly FrontedV3Property<string> FirstProperty =
        new("Appearance.TextColor", FrontedV3Storage.ExtensionData("TextColor"));

    /// <summary>
    /// 第二条属性，OptionsPath 与第一条重复。
    /// </summary>
    public static readonly FrontedV3Property<string> SecondProperty =
        new("Appearance.TextColor", FrontedV3Storage.ExtensionData("OtherColor"));
}

/// <summary>
/// 属性 Storage 指向保留字段 Left，用于测试保留字段拒绝。
/// </summary>
[FrontedV3Control("ReservedStorage")]
public sealed class ControlWithReservedStorage : FrontedV3ControlBase
{
    /// <summary>
    /// 试图覆盖保留字段 Left 的属性。
    /// </summary>
    public static readonly FrontedV3Property<double> LeftProperty =
        new("Custom.Left", FrontedV3Storage.ClrProperty("Left"));
}

/// <summary>
/// ControlId 为完整 canonical ID 形式，用于测试不安全 ID 拒绝。
/// </summary>
[FrontedV3Control("plugin:pkg/ctrl")]
public sealed class ControlWithFullCanonicalId : FrontedV3ControlBase
{
}

/// <summary>
/// 插件控件试图设置 IsBuiltIn=true，用于测试插件不能伪装内置控件。
/// </summary>
[FrontedV3Control("FakeBuiltIn", IsBuiltIn = true)]
public sealed class PluginControlClaimingBuiltIn : FrontedV3ControlBase
{
}

/// <summary>
/// 属性 OptionsPath 使用禁止的 Options. 前缀，用于测试禁止路径拒绝。
/// </summary>
[FrontedV3Control("ForbiddenPath")]
public sealed class ControlWithForbiddenOptionsPath : FrontedV3ControlBase
{
    /// <summary>
    /// 使用禁止的 OptionsPath。
    /// </summary>
    public static readonly FrontedV3Property<string> LayoutProperty =
        new("Options.Layout", FrontedV3Storage.ExtensionData("Layout"));
}
