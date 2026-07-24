using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.Registry;
using System;
using System.Linq;
using System.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedV3LayoutWindowIdentityTest
{
    [Fact]
    public void BuildCanonicalId_BuiltIn_ReturnsLocalId()
    {
        Assert.Equal(
            "BpWindow",
            FrontedV3LayoutWindowIdentity.BuildCanonicalId("BpWindow", packageId: null, isBuiltIn: true));
    }

    [Fact]
    public void BuildCanonicalId_PluginA_PrefixedWithPluginAndPackage()
    {
        Assert.Equal(
            "plugin:a/Overlay",
            FrontedV3LayoutWindowIdentity.BuildCanonicalId("Overlay", "a", isBuiltIn: false));
    }

    [Fact]
    public void BuildCanonicalId_PluginB_PrefixedWithPluginAndPackage()
    {
        Assert.Equal(
            "plugin:b/Overlay",
            FrontedV3LayoutWindowIdentity.BuildCanonicalId("Overlay", "b", isBuiltIn: false));
    }

    [Fact]
    public void BuildCanonicalId_BuiltInIgnoresPackageIdEvenWhenProvided()
    {
        Assert.Equal(
            "BpWindow",
            FrontedV3LayoutWindowIdentity.BuildCanonicalId("BpWindow", "some-package", isBuiltIn: true));
    }

    [Fact]
    public void BuildCanonicalId_NullPackageIdForNonBuiltIn_ReturnsLocalId()
    {
        Assert.Equal(
            "Overlay",
            FrontedV3LayoutWindowIdentity.BuildCanonicalId("Overlay", packageId: null, isBuiltIn: false));
    }

    [Theory]
    [InlineData("plugin:x/y")]
    [InlineData("../escape")]
    [InlineData("a.b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \n ")]
    public void EnsureValidLocalWindowId_RejectsInvalidIds(string localWindowId)
    {
        Assert.False(FrontedV3LayoutWindowIdValidator.IsValidLocalWindowId(localWindowId));
        Assert.Throws<ArgumentException>(
            () => FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId(localWindowId));
    }

    [Fact]
    public void EnsureValidLocalWindowId_RejectsNull()
    {
        Assert.False(FrontedV3LayoutWindowIdValidator.IsValidLocalWindowId(null!));
        Assert.Throws<ArgumentException>(
            () => FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId(null!));
    }

    [Theory]
    [InlineData("BpWindow")]
    [InlineData("ExampleOverlay")]
    [InlineData("a-b_c")]
    public void EnsureValidLocalWindowId_AcceptsValidIds(string localWindowId)
    {
        Assert.True(FrontedV3LayoutWindowIdValidator.IsValidLocalWindowId(localWindowId));
        FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId(localWindowId);
    }

    [Fact]
    public void EnsureValidLocalWindowId_ExceptionMessageContainsValueAndReason()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId("a.b"));

        Assert.Contains("a.b", exception.Message, StringComparison.Ordinal);
        Assert.Contains(".", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Task 2.7：纯空白 Local ID 在注册 API 入口 <c>AddFrontedV3LayoutWindow</c> 必须被拒绝，
    /// 抛出 <see cref="ArgumentException"/>，而不是 warning 后静默跳过。
    /// </summary>
    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \n ")]
    public void WhitespaceLocalId_IsRejected(string whitespaceId)
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<ArgumentException>(
            () => services.AddFrontedV3LayoutWindow(whitespaceId, isBuiltIn: false));

        // 异常信息应明确指出 Local ID 为空白。
        Assert.Contains("non-whitespace", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Task 2.7：当在插件作用域内（PackageId 非空）调用 <c>AddFrontedV3LayoutWindow</c>，
    /// 且 PackageId 不是安全的 canonical path segment（含路径分隔符、<c>..</c> 等）时，
    /// 应抛出 <see cref="ArgumentException"/>。
    /// </summary>
    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    public void UnsafePackageId_IsRejected(string unsafePackageId)
    {
        var services = new ServiceCollection();

        // 使用合法的 Local ID，使验证进行到 PackageId 校验阶段。
        using (FrontedPluginRegistrationContext.BeginScope(unsafePackageId))
        {
            var ex = Assert.Throws<ArgumentException>(
                () => services.AddFrontedV3LayoutWindow("ValidLocalId", isBuiltIn: false));

            Assert.Contains("package id", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(unsafePackageId, ex.Message, StringComparison.Ordinal);
        }
    }

    // ===== Task 5.1 Identity 测试矩阵补齐 =====

    /// <summary>
    /// Task 5.1：插件 XAML 窗口注册后，registration.LocalId 应为 attribute 上的原始 GUID，
    /// 且 canonical ID 中包含该原始 GUID。这验证插件 XAML 窗口的身份基于 attribute GUID。
    /// </summary>
    [Fact]
    public void PluginXaml_UsesRawAttributeGuid()
    {
        const string attributeGuid = "3363BFE1-1393-4765-B926-001B6848FAF7";
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope("test.plugin"))
        {
            services.AddFrontedWindow<PluginXamlTestWindow, PluginXamlTestViewModel>();
        }

        var registration = services
            .BuildServiceProvider()
            .GetServices<FrontedWindowRegistration>()
            .OfType<FrontedXamlWindowRegistration>()
            .Single();

        Assert.Equal(attributeGuid, registration.LocalId);
        Assert.Contains(attributeGuid, registration.Id, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(FrontedWindowRegistrationKind.Xaml, registration.Kind);
    }

    /// <summary>
    /// Task 5.1：插件 XAML 窗口的 PackageId 仅作为来源元数据存储在 registration.PackageId 上，
    /// 且 XAML 窗口不参与 v3 layout / Designer（不出现在 GetV3LayoutWindows 中）。
    /// </summary>
    [Fact]
    public void PluginXaml_PackageIdIsMetadataOnly()
    {
        const string packageId = "test.plugin";
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope(packageId))
        {
            services.AddFrontedWindow<PluginXamlTestWindow, PluginXamlTestViewModel>();
        }

        var provider = services.BuildServiceProvider();
        var registration = provider
            .GetServices<FrontedWindowRegistration>()
            .OfType<FrontedXamlWindowRegistration>()
            .Single();

        // PackageId 作为来源元数据存储。
        Assert.Equal(packageId, registration.PackageId);
        Assert.False(registration.IsBuiltIn);
        Assert.Equal(FrontedWindowRegistrationKind.Xaml, registration.Kind);

        // XAML 窗口不参与 v3 layout / Designer：GetV3LayoutWindows 不返回 XAML 注册。
        var registry = new FrontedWindowRegistryService(
            provider.GetServices<FrontedWindowRegistration>());
        Assert.Empty(registry.GetV3LayoutWindows());
    }

    /// <summary>
    /// Task 5.1：XAML 窗口 attribute ID 为空时，<c>AddFrontedWindow</c> 必须在注册入口抛出
    /// <see cref="ArgumentException"/>，不得静默跳过。
    /// </summary>
    [Fact]
    public void Xaml_RejectsEmptyId()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<ArgumentException>(
            () => services.AddFrontedWindow<EmptyIdXamlTestWindow, PluginXamlTestViewModel>());

        Assert.Contains("ID", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Task 5.1：插件 v3 窗口通过 <c>AddFrontedV3LayoutWindow</c> 在插件作用域内注册时，
    /// canonical ID 应使用 <c>plugin:{PackageId}/{LocalId}</c> 命名空间格式。
    /// </summary>
    [Fact]
    public void PluginV3_UsesNamespacedCanonicalId()
    {
        const string localId = "Overlay";
        const string packageId = "test.plugin";
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope(packageId))
        {
            services.AddFrontedV3LayoutWindow(localId, isBuiltIn: false);
        }

        var registration = services
            .BuildServiceProvider()
            .GetServices<FrontedWindowRegistration>()
            .OfType<FrontedV3LayoutWindowRegistration>()
            .Single();

        Assert.Equal($"plugin:{packageId}/{localId}", registration.Id);
        Assert.Equal(localId, registration.LocalId);
        Assert.Equal(packageId, registration.PackageId);
        Assert.False(registration.IsBuiltIn);
    }

    /// <summary>
    /// Task 5.1：内置 v3 窗口通过 <c>AddFrontedV3LayoutWindow</c> 注册时（isBuiltIn=true），
    /// canonical ID 应直接使用 LocalId，不添加 plugin: 前缀。
    /// </summary>
    [Fact]
    public void BuiltInV3_UsesLocalId()
    {
        const string localId = "BpWindow";
        var services = new ServiceCollection();
        services.AddFrontedV3LayoutWindow(localId, isBuiltIn: true);

        var registration = services
            .BuildServiceProvider()
            .GetServices<FrontedWindowRegistration>()
            .OfType<FrontedV3LayoutWindowRegistration>()
            .Single();

        Assert.Equal(localId, registration.Id);
        Assert.Equal(localId, registration.LocalId);
        Assert.Null(registration.PackageId);
        Assert.True(registration.IsBuiltIn);
    }

    /// <summary>
    /// Task 5.1：两个不同插件可以注册相同 LocalId 的窗口，因为 canonical ID 通过
    /// <c>plugin:{PackageId}/{LocalId}</c> 命名空间隔离。
    /// </summary>
    [Fact]
    public void DifferentPlugins_CanUseSameLocalId()
    {
        const string localId = "Overlay";
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope("plugin.a"))
        {
            services.AddFrontedV3LayoutWindow(localId, isBuiltIn: false);
        }

        using (FrontedPluginRegistrationContext.BeginScope("plugin.b"))
        {
            services.AddFrontedV3LayoutWindow(localId, isBuiltIn: false);
        }

        var registrations = services
            .BuildServiceProvider()
            .GetServices<FrontedWindowRegistration>()
            .OfType<FrontedV3LayoutWindowRegistration>()
            .ToArray();

        Assert.Equal(2, registrations.Length);
        Assert.All(registrations, r => Assert.Equal(localId, r.LocalId));
        Assert.All(registrations, r => Assert.False(r.IsBuiltIn));
        // 两个 canonical ID 应不同。
        Assert.NotEqual(registrations[0].Id, registrations[1].Id);
        Assert.Contains("plugin.a", registrations[0].Id);
        Assert.Contains("plugin.b", registrations[1].Id);

        // 构造 registry 不应抛出重复 ID 异常。
        var registry = new FrontedWindowRegistryService(registrations);
        Assert.Equal(2, registry.GetV3LayoutWindows().Count());
    }

    /// <summary>
    /// Task 5.1：Registry 构造时，两个仅大小写不同的 canonical ID 应被视为重复，
    /// 抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    [Fact]
    public void CanonicalDuplicate_IsCaseInsensitive()
    {
        var upper = new FrontedV3LayoutWindowRegistration
        {
            Id = "plugin:test/Overlay",
            LocalId = "Overlay",
            IsBuiltIn = false,
            DisplayName = "Overlay"
        };
        var lower = new FrontedV3LayoutWindowRegistration
        {
            Id = "plugin:test/overlay",
            LocalId = "overlay",
            IsBuiltIn = false,
            DisplayName = "overlay"
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new FrontedWindowRegistryService(new[] { upper, lower }));

        Assert.Contains("plugin:test/Overlay", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 用于测试插件 XAML 窗口注册的测试窗口类，使用合法 GUID 作为 attribute ID。
/// </summary>
[FrontedWindowInfo("3363BFE1-1393-4765-B926-001B6848FAF7", "Plugin XAML Test Window")]
internal sealed class PluginXamlTestWindow : Window
{
}

/// <summary>
/// 用于测试插件 XAML 窗口注册的测试窗口类，使用空字符串作为 attribute ID。
/// </summary>
[FrontedWindowInfo("", "Empty Id Test Window")]
internal sealed class EmptyIdXamlTestWindow : Window
{
}

/// <summary>
/// 用于测试 XAML 窗口注册的测试 ViewModel。
/// </summary>
internal sealed class PluginXamlTestViewModel : ViewModelBase;
