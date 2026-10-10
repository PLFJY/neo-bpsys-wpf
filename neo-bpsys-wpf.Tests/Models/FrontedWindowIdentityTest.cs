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

public class FrontedWindowIdentityTest
{
    /// <summary>窗口身份按内置来源与插件包作用域生成稳定的公开 ID。</summary>
    /// <param name="localId">局部 ID。</param>
    /// <param name="packageId">插件包 ID。</param>
    /// <param name="isBuiltIn">是否为内置窗口。</param>
    /// <param name="expected">预期 canonical ID。</param>
    [Theory]
    [InlineData("BpWindow", null, true, "BpWindow")]
    [InlineData("Overlay", "a", false, "plugin:a/Overlay")]
    [InlineData("Overlay", "b", false, "plugin:b/Overlay")]
    [InlineData("BpWindow", "some-package", true, "BpWindow")]
    [InlineData("Overlay", null, false, "Overlay")]
    public void BuildCanonicalId_RespectsBuiltInAndPackageScope(
        string localId, string? packageId, bool isBuiltIn, string expected)
    {
        Assert.Equal(expected, FrontedWindowIdentity.BuildCanonicalId(localId, packageId, isBuiltIn));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("plugin:x/y")]
    [InlineData("../escape")]
    [InlineData("a.b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \n ")]
    public void EnsureValidLocalWindowId_RejectsInvalidIds(string? localWindowId)
    {
        Assert.False(FrontedV3LayoutWindowIdValidator.IsValidLocalWindowId(localWindowId!));
        Assert.Throws<ArgumentException>(
            () => FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId(localWindowId!));
    }

    [Theory]
    [InlineData("BpWindow")]
    [InlineData("ExampleOverlay")]
    [InlineData("a-b_c")]
    public void EnsureValidLocalWindowId_AcceptsValidIds(string localWindowId)
    {
        Assert.True(FrontedV3LayoutWindowIdValidator.IsValidLocalWindowId(localWindowId!));
        FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId(localWindowId!);
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

    // ===== XAML 局部 ID 安全校验（EnsureValidWindowLocalId）测试 =====

    /// <summary>
    /// XAML 窗口局部 ID 不要求为 GUID；任意不含路径分隔符、冒号、控制字符且无前后空白的
    /// 稳定字符串都应通过 <see cref="FrontedWindowIdentity.EnsureValidWindowLocalId"/>。
    /// </summary>
    [Theory]
    [InlineData("CommunityScoreOverlay")]
    [InlineData("community.score-overlay")]
    [InlineData("社区比分窗口")]
    [InlineData("Overlay 2")]
    [InlineData("3363BFE1-1393-4765-B926-001B6848FAF7")]
    public void Xaml_NonGuidSafeId_IsAccepted(string id)
    {
        var exception = Record.Exception(() => FrontedWindowIdentity.EnsureValidWindowLocalId(id));

        Assert.Null(exception);
    }

    /// <summary>拒绝会破坏 canonical ID 或路径安全的 XAML 窗口局部 ID。</summary>
    /// <param name="id">不安全的局部 ID。</param>
    [Theory]
    [InlineData("foo/bar")]
    [InlineData("foo\\bar")]
    [InlineData("foo:bar")]
    [InlineData("abc\tdef")]
    [InlineData(" foo")]
    [InlineData("foo ")]
    public void Xaml_UnsafeLocalId_IsRejected(string id)
    {
        Assert.Throws<ArgumentException>(() => FrontedWindowIdentity.EnsureValidWindowLocalId(id));
    }

    /// <summary>
    /// 两个不同插件包注册相同 LocalId 的 XAML 窗口时，canonical ID 通过
    /// <c>plugin:{PackageId}/{LocalId}</c> 命名空间隔离，应产生不同且不相等的 canonical ID。
    /// </summary>
    [Fact]
    public void PluginXaml_SameLocalIdAcrossPackages_IsNamespaced()
    {
        const string localId = "Overlay";
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope("pkgA"))
        {
            services.AddFrontedWindow<OverlayXamlTestWindow, PluginXamlTestViewModel>();
        }

        using (FrontedPluginRegistrationContext.BeginScope("pkgB"))
        {
            services.AddFrontedWindow<OverlayXamlTestWindow, PluginXamlTestViewModel>();
        }

        var registrations = services
            .BuildServiceProvider()
            .GetServices<FrontedWindowRegistration>()
            .OfType<FrontedXamlWindowRegistration>()
            .ToArray();

        Assert.Equal(2, registrations.Length);
        Assert.All(registrations, r => Assert.Equal(localId, r.LocalId));

        Assert.Equal("plugin:pkgA/Overlay", registrations[0].Id);
        Assert.Equal("plugin:pkgB/Overlay", registrations[1].Id);
        Assert.NotEqual(registrations[0].Id, registrations[1].Id);
    }

    /// <summary>
    /// 同一插件包内注册两个仅大小写不同的 XAML 窗口局部 ID（如 <c>OVERLAY</c> 与 <c>overlay</c>），
    /// 生成的 canonical ID 仅大小写不同，<see cref="FrontedWindowRegistryService"/> 构造时应
    /// fail-fast 抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    [Fact]
    public void PluginXaml_DuplicateIdIgnoringCase_FailsFast()
    {
        const string packageId = "test.plugin";
        var services = new ServiceCollection();

        using (FrontedPluginRegistrationContext.BeginScope(packageId))
        {
            services.AddFrontedWindow<UpperOverlayXamlTestWindow, PluginXamlTestViewModel>();
            services.AddFrontedWindow<LowerOverlayXamlTestWindow, PluginXamlTestViewModel>();
        }

        var registrations = services
            .BuildServiceProvider()
            .GetServices<FrontedWindowRegistration>()
            .OfType<FrontedXamlWindowRegistration>()
            .ToArray();

        // 两个 registration 均应存在，canonical ID 仅大小写不同。
        Assert.Equal(2, registrations.Length);
        Assert.Equal($"plugin:{packageId}/OVERLAY", registrations[0].Id);
        Assert.Equal($"plugin:{packageId}/overlay", registrations[1].Id);

        var ex = Assert.Throws<InvalidOperationException>(
            () => new FrontedWindowRegistryService(registrations));

        Assert.Contains($"plugin:{packageId}/OVERLAY", ex.Message, StringComparison.OrdinalIgnoreCase);
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

/// <summary>
/// 用于测试跨插件包命名空间隔离的 XAML 窗口，使用非 GUID 的安全局部 ID。
/// </summary>
[FrontedWindowInfo("Overlay", "Overlay XAML Test Window")]
internal sealed class OverlayXamlTestWindow : Window
{
}

/// <summary>
/// 用于测试大小写重复检测的 XAML 窗口，使用大写局部 ID。
/// </summary>
[FrontedWindowInfo("OVERLAY", "Upper Overlay XAML Test Window")]
internal sealed class UpperOverlayXamlTestWindow : Window
{
}

/// <summary>
/// 用于测试大小写重复检测的 XAML 窗口，使用小写局部 ID。
/// </summary>
[FrontedWindowInfo("overlay", "Lower Overlay XAML Test Window")]
internal sealed class LowerOverlayXamlTestWindow : Window
{
}
