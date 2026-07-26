#nullable enable

using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.Registry;
using System;
using System.Linq;
using System.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 验证 <see cref="FrontedDesignerLayoutCatalog"/> 的契约（P1-2 / Task 2.2）：
/// 必须接收非空 Registry、只读取 v3 注册、不暴露 XAML 注册、无硬编码 fallback。
/// </summary>
public class FrontedDesignerLayoutCatalogTest
{
    [Fact]
    public void DesignerCatalog_RequiresRegistry()
    {
        Assert.Throws<ArgumentNullException>(() => new FrontedDesignerLayoutCatalog(null!));
    }

    [Fact]
    public void DesignerCatalog_ContainsOnlyV3Registrations()
    {
        var v3 = CreateV3Registration("BpWindow", isBuiltIn: true);
        var xaml = CreateXamlRegistration("XamlOnly", isBuiltIn: true);
        var registry = new FrontedWindowRegistryService(new FrontedWindowRegistration[] { v3, xaml });

        var catalog = new FrontedDesignerLayoutCatalog(registry);
        var entries = catalog.GetEntries();

        Assert.Single(entries);
        Assert.Equal("BpWindow", entries[0].CanonicalWindowId);
    }

    [Fact]
    public void DesignerCatalog_DoesNotContainXaml()
    {
        var xaml = CreateXamlRegistration("ScoreXaml", isBuiltIn: true);
        var registry = new FrontedWindowRegistryService(new FrontedWindowRegistration[] { xaml });

        var catalog = new FrontedDesignerLayoutCatalog(registry);
        var entries = catalog.GetEntries();

        Assert.Empty(entries);
    }

    [Fact]
    public void DesignerCatalog_HasNoHardcodedFallback()
    {
        // 空 Registry 不应触发任何硬编码内置窗口清单。
        var registry = new FrontedWindowRegistryService(Array.Empty<FrontedWindowRegistration>());

        var catalog = new FrontedDesignerLayoutCatalog(registry);
        var entries = catalog.GetEntries();

        Assert.Empty(entries);
        Assert.DoesNotContain(entries, entry => entry.CanonicalWindowId == "BpWindow");
        Assert.DoesNotContain(entries, entry => entry.CanonicalWindowId == "ScoreSurWindow");
        Assert.DoesNotContain(entries, entry => entry.CanonicalWindowId == "ScoreHunWindow");
        Assert.DoesNotContain(entries, entry => entry.CanonicalWindowId == "ScoreGlobalWindow");
        Assert.DoesNotContain(entries, entry => entry.CanonicalWindowId == "CutSceneWindow");
        Assert.DoesNotContain(entries, entry => entry.CanonicalWindowId == "GameDataWindow");
        Assert.DoesNotContain(entries, entry => entry.CanonicalWindowId == "BpOverviewWindow");
        Assert.DoesNotContain(entries, entry => entry.CanonicalWindowId == "MapV2Window");
    }

    /// <summary>
    /// Task 5.3：插件 v3 窗口注册（canonical ID = plugin:{PackageId}/{LocalId}）应出现在
    /// Designer catalog 中，且其 <see cref="FrontedDesignerLayoutCatalogEntry.CanonicalWindowId"/>
    /// 使用完整的 canonical ID，而非仅 LocalId。
    /// </summary>
    [Fact]
    public void PluginV3_UsesCanonicalId()
    {
        const string packageId = "test.plugin";
        const string localId = "Overlay";
        var canonicalId = $"plugin:{packageId}/{localId}";

        var v3 = new FrontedV3LayoutWindowRegistration
        {
            Id = canonicalId,
            LocalId = localId,
            IsBuiltIn = false,
            PackageId = packageId,
            DisplayName = localId
        };
        var registry = new FrontedWindowRegistryService(new FrontedWindowRegistration[] { v3 });

        var catalog = new FrontedDesignerLayoutCatalog(registry);
        var entries = catalog.GetEntries();

        var entry = Assert.Single(entries);
        Assert.Equal(canonicalId, entry.CanonicalWindowId);
        Assert.NotEqual(localId, entry.CanonicalWindowId);
    }

    private static FrontedV3LayoutWindowRegistration CreateV3Registration(string id, bool isBuiltIn)
    {
        return new FrontedV3LayoutWindowRegistration
        {
            Id = id,
            LocalId = id,
            IsBuiltIn = isBuiltIn,
            DisplayName = id
        };
    }

    private static FrontedXamlWindowRegistration CreateXamlRegistration(string id, bool isBuiltIn)
    {
        return new FrontedXamlWindowRegistration
        {
            Id = id,
            LocalId = id,
            IsBuiltIn = isBuiltIn,
            DisplayName = id,
            WindowType = typeof(Window)
        };
    }
}
