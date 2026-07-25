using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.Registry;
using System;
using System.Collections.Generic;
using System.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 验证 <see cref="FrontedWindowRegistryService"/> 在构造时对重复 Canonical ID 的 fail-fast 行为，
/// 以及 Canonical ID 大小写不敏感比较和空 ID fail-fast 行为（Task 1.1）。
/// </summary>
public class FrontedWindowRegistryServiceTest
{
    [Fact]
    public void Constructor_DuplicateCanonicalId_ThrowsInvalidOperationExceptionWithConflictingId()
    {
        const string duplicateId = "plugin:top.plfjy.test/Overlay";
        var first = CreateV3Registration(duplicateId, "FirstWindow");
        var second = CreateV3Registration(duplicateId, "SecondWindow");

        var registrations = new List<FrontedWindowRegistration> { first, second };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new FrontedWindowRegistryService(registrations));

        Assert.Contains(duplicateId, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_DuplicateCanonicalIdAcrossKinds_ThrowsInvalidOperationExceptionWithConflictingId()
    {
        const string duplicateId = "BpWindow";
        var first = CreateV3Registration(duplicateId, "BpWindow");
        var second = CreateXamlRegistration(duplicateId, "BpWindowXaml");

        var registrations = new List<FrontedWindowRegistration> { first, second };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new FrontedWindowRegistryService(registrations));

        Assert.Contains(duplicateId, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Task 1.1：两个 registration ID 仅大小写不同时，构造应 fail-fast 抛出
    /// <see cref="InvalidOperationException"/>，因为注册表使用 OrdinalIgnoreCase 比较语义。
    /// </summary>
    [Fact]
    public void Registry_RejectsCanonicalIdsDifferingOnlyByCase()
    {
        var upper = CreateV3Registration("BpWindow", "BpWindow");
        var lower = CreateV3Registration("bpwindow", "BpWindowLower");

        var registrations = new List<FrontedWindowRegistration> { upper, lower };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new FrontedWindowRegistryService(registrations));

        // 异常信息应包含被冲突的 ID（以任一大小写形式）。
        Assert.Contains("BpWindow", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Task 1.1：注册表 TryGet 使用 OrdinalIgnoreCase，注册 "BpWindow" 后用 "bpwindow" 应能命中。
    /// </summary>
    [Fact]
    public void Registry_TryGet_IsOrdinalIgnoreCase()
    {
        var registration = CreateV3Registration("BpWindow", "BP 窗口");
        var registry = new FrontedWindowRegistryService(new[] { registration });

        var found = registry.TryGet("bpwindow", out var resolved);

        Assert.True(found);
        Assert.Same(registration, resolved);
        // 返回的 registration.Id 保留注册时的 canonical 形式。
        Assert.Equal("BpWindow", resolved.Id);
    }

    /// <summary>
    /// Task 1.1：registration 的 Id 为空字符串/null/纯空白时，构造应 fail-fast 抛出
    /// <see cref="InvalidOperationException"/>，异常信息明确说明 ID 为空。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registry_EmptyIdFailsFast(string emptyId)
    {
        var registration = CreateV3Registration(emptyId, "EmptyWindow");

        var registrations = new List<FrontedWindowRegistration> { registration };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new FrontedWindowRegistryService(registrations));

        // 异常信息应明确说明 ID 为空。
        Assert.Contains("empty Canonical ID", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Task 5.2：使用大小写不同的 Canonical ID 变体进行查找时，应返回同一 registration 实例，
    /// 验证注册表的 OrdinalIgnoreCase 语义在 lookup 路径上一致。
    /// </summary>
    [Fact]
    public void CaseVariantLookup_UsesSameRegistration()
    {
        var registration = CreateV3Registration("BpWindow", "BP 窗口");
        var registry = new FrontedWindowRegistryService(new[] { registration });

        var foundUpper = registry.TryGet("BpWindow", out var resolvedUpper);
        var foundLower = registry.TryGet("bpwindow", out var resolvedLower);
        var foundMixed = registry.TryGet("BPWINDOW", out var resolvedMixed);

        Assert.True(foundUpper);
        Assert.True(foundLower);
        Assert.True(foundMixed);
        Assert.Same(registration, resolvedUpper);
        Assert.Same(registration, resolvedLower);
        Assert.Same(registration, resolvedMixed);
    }

    /// <summary>
    /// 验证 <see cref="FrontedWindowRegistryService.GetManageableWindows"/> 保持 DI 注册顺序，
    /// 不再按 <see cref="FrontedWindowRegistration.LocalId"/> 字母排序。
    /// 注册顺序刻意与字母顺序不同，以使测试能够区分两种排序策略。
    /// </summary>
    [Fact]
    public void GetManageableWindows_PreservesRegistrationOrder()
    {
        var first = CreateV3Registration("WindowC", "窗口 C");
        var second = CreateV3Registration("WindowA", "窗口 A");
        var third = CreateV3Registration("WindowB", "窗口 B");

        var registrations = new List<FrontedWindowRegistration> { first, second, third };
        var registry = new FrontedWindowRegistryService(registrations);

        var manageable = registry.GetManageableWindows();

        Assert.Equal(3, manageable.Count);
        Assert.Same(first, manageable[0]);
        Assert.Same(second, manageable[1]);
        Assert.Same(third, manageable[2]);
    }

    /// <summary>
    /// 验证 <see cref="FrontedWindowRegistryService.GetManageableWindows"/> 保持 DI 注册顺序：
    /// 内置窗口先于插件窗口注册时，内置窗口排在前面。
    /// 插件窗口的 LocalId 字母序刻意早于内置窗口，以验证不再按字母排序。
    /// </summary>
    [Fact]
    public void GetManageableWindows_BuiltInBeforePlugin()
    {
        var builtIn = CreateV3Registration("Zeta", "内置窗口", isBuiltIn: true, packageId: null);
        var plugin = CreateV3Registration("plugin:test.plugin/Alpha", "插件窗口", isBuiltIn: false, packageId: "test.plugin");

        var registrations = new List<FrontedWindowRegistration> { builtIn, plugin };
        var registry = new FrontedWindowRegistryService(registrations);

        var manageable = registry.GetManageableWindows();

        Assert.Equal(2, manageable.Count);
        Assert.Same(builtIn, manageable[0]);
        Assert.Same(plugin, manageable[1]);
        Assert.True(manageable[0].IsBuiltIn);
        Assert.False(manageable[1].IsBuiltIn);
    }

    private static FrontedV3LayoutWindowRegistration CreateV3Registration(string id, string displayName)
    {
        return new FrontedV3LayoutWindowRegistration
        {
            Id = id,
            LocalId = id,
            IsBuiltIn = false,
            DisplayName = displayName
        };
    }

    private static FrontedV3LayoutWindowRegistration CreateV3Registration(
        string id,
        string displayName,
        bool isBuiltIn,
        string? packageId)
    {
        return new FrontedV3LayoutWindowRegistration
        {
            Id = id,
            LocalId = id,
            IsBuiltIn = isBuiltIn,
            PackageId = packageId,
            DisplayName = displayName
        };
    }

    private static FrontedXamlWindowRegistration CreateXamlRegistration(string id, string displayName)
    {
        return new FrontedXamlWindowRegistration
        {
            Id = id,
            LocalId = id,
            IsBuiltIn = false,
            DisplayName = displayName,
            WindowType = typeof(Window)
        };
    }
}
