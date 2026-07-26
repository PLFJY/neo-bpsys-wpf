using System;
using System.Collections;
using System.Collections.Generic;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 测试 <see cref="FrontedV3PartDefinitionValidator"/> 在控件注册时对 Part/PartCollection
/// 声明的 fail-fast 校验，覆盖 Id 合法性、唯一性、Capabilities/Storage 配对与策略/回调配对。
/// </summary>
/// <remarks>
/// <para>
/// 这些测试验证 Designer V3 验收 Round-2 P1-任务 10 的契约：
/// <list type="bullet">
/// <item>Part/PartCollection Id 必须非空、非空白、不含路径分隔符或文件系统非法字符。</item>
/// <item>同一控件内的 Part Id 与 PartCollection Id 必须唯一且不得跨集合冲突。</item>
/// <item>Part Capabilities 与 Storage 必须配对（CanMove→至少一个 X/Y Storage；CanResize→至少一个 W/H Storage）。</item>
/// <item>PartCollection FixedTemplate 策略必须配 EnsureTemplateItems。</item>
/// <item>PartCollection 声明具名 Templates 时必须配 ApplyTemplate。</item>
/// </list>
/// </para>
/// <para>
/// 这些是注册时契约测试，不涉及 WPF 视觉树，因此不需要
/// <see cref="neo_bpsys_wpf.Tests.Infrastructure.WpfTestThread"/>。
/// </para>
/// </remarks>
public class FrontedV3PartDefinitionValidatorTest
{
    private static readonly Type DummyControlType = typeof(DummyValidatorControl);

    // -------------------------------------------------------------------
    // 1. Empty/Whitespace Part Id
    // -------------------------------------------------------------------

    /// <summary>
    /// Part Id 为空字符串时，<see cref="FrontedV3PartDefinitionValidator.ValidateParts"/> 必须抛出
    /// <see cref="FrontedLayoutConfigException"/>，禁止空 Id 进入 Registration。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ValidateParts_RejectsEmptyOrWhitespacePartId(string id)
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(id: id, capabilities: FrontedV3PartCapabilities.Resize)
            {
                WidthStorage = FrontedV3Storage.ClrProperty("W"),
                HeightStorage = FrontedV3Storage.ClrProperty("H")
            }
        };

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => FrontedV3PartDefinitionValidator.ValidateParts(parts, DummyControlType));
        Assert.Contains("Part Id", ex.Message, StringComparison.Ordinal);
        Assert.Contains("non-empty", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 2. Part Id with unsafe characters
    // -------------------------------------------------------------------

    /// <summary>
    /// Part Id 包含路径分隔符或文件系统非法字符时，必须抛出
    /// <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Theory]
    [InlineData("Part/WithSlash")]
    [InlineData("Part\\WithBackslash")]
    [InlineData("Part:WithColon")]
    [InlineData("Part|WithPipe")]
    public void ValidateParts_RejectsUnsafeCharactersInPartId(string id)
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(id: id, capabilities: FrontedV3PartCapabilities.None)
        };

        Assert.Throws<FrontedLayoutConfigException>(
            () => FrontedV3PartDefinitionValidator.ValidateParts(parts, DummyControlType));
    }

    // -------------------------------------------------------------------
    // 3. Duplicate Part Id
    // -------------------------------------------------------------------

    /// <summary>
    /// 同一控件内出现重复 Part Id 时，必须抛出 <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Fact]
    public void ValidateParts_RejectsDuplicatePartId()
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(id: "Logo", capabilities: FrontedV3PartCapabilities.None),
            new FrontedV3PartDefinition(id: "Logo", capabilities: FrontedV3PartCapabilities.None)
        };

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => FrontedV3PartDefinitionValidator.ValidateParts(parts, DummyControlType));
        Assert.Contains("duplicate Part Id 'Logo'", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 4. CanMove without X/Y Storage
    // -------------------------------------------------------------------

    /// <summary>
    /// Part 声明 <see cref="FrontedV3PartCapabilities.CanMove"/> 但未配置任何 XStorage/YStorage 时，
    /// 必须抛出 <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Fact]
    public void ValidateParts_RejectsCanMoveWithoutXOrYStorage()
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(id: "Movable", capabilities: FrontedV3PartCapabilities.Move)
        };

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => FrontedV3PartDefinitionValidator.ValidateParts(parts, DummyControlType));
        Assert.Contains("CanMove=true", ex.Message, StringComparison.Ordinal);
        Assert.Contains("XStorage and YStorage", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 5. CanResize without Width/Height Storage
    // -------------------------------------------------------------------

    /// <summary>
    /// Part 声明 <see cref="FrontedV3PartCapabilities.CanResize"/> 但未配置任何 WidthStorage/HeightStorage 时，
    /// 必须抛出 <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Fact]
    public void ValidateParts_RejectsCanResizeWithoutWidthOrHeightStorage()
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(id: "Resizable", capabilities: FrontedV3PartCapabilities.Resize)
        };

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => FrontedV3PartDefinitionValidator.ValidateParts(parts, DummyControlType));
        Assert.Contains("CanResize=true", ex.Message, StringComparison.Ordinal);
        Assert.Contains("WidthStorage and HeightStorage", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 6. CanMove with only XStorage is valid
    // -------------------------------------------------------------------

    /// <summary>
    /// Part 声明 <see cref="FrontedV3PartCapabilities.CanMove"/> 且配置了 XStorage（即使没有 YStorage）
    /// 时必须通过校验，因为水平移动是合法的子集。
    /// </summary>
    [Fact]
    public void ValidateParts_AcceptsCanMoveWithOnlyXStorage()
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(
                id: "HorizontalOnly",
                capabilities: FrontedV3PartCapabilities.Move,
                xStorage: FrontedV3Storage.ClrProperty("X"))
        };

        var exception = Record.Exception(
            () => FrontedV3PartDefinitionValidator.ValidateParts(parts, DummyControlType));
        Assert.Null(exception);
    }

    // -------------------------------------------------------------------
    // 7. CanResize with only WidthStorage is valid
    // -------------------------------------------------------------------

    /// <summary>
    /// Part 声明 <see cref="FrontedV3PartCapabilities.CanResize"/> 且配置了 WidthStorage（即使没有 HeightStorage）
    /// 时必须通过校验，因为水平缩放是合法的子集。
    /// </summary>
    [Fact]
    public void ValidateParts_AcceptsCanResizeWithOnlyWidthStorage()
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(
                id: "WidthOnly",
                capabilities: FrontedV3PartCapabilities.Resize,
                widthStorage: FrontedV3Storage.ClrProperty("Width"))
        };

        var exception = Record.Exception(
            () => FrontedV3PartDefinitionValidator.ValidateParts(parts, DummyControlType));
        Assert.Null(exception);
    }

    // -------------------------------------------------------------------
    // 8. None capabilities without any Storage is valid
    // -------------------------------------------------------------------

    /// <summary>
    /// Part 声明 <see cref="FrontedV3PartCapabilities.None"/> 且未配置任何 Storage 时必须通过校验，
    /// 因为这种 Part 仅用于 Visual 发现与显示，不参与几何操作。
    /// </summary>
    [Fact]
    public void ValidateParts_AcceptsNoneCapabilitiesWithoutStorage()
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(id: "Display", capabilities: FrontedV3PartCapabilities.None)
        };

        var exception = Record.Exception(
            () => FrontedV3PartDefinitionValidator.ValidateParts(parts, DummyControlType));
        Assert.Null(exception);
    }

    // -------------------------------------------------------------------
    // 9. Empty/Whitespace PartCollection Id
    // -------------------------------------------------------------------

    /// <summary>
    /// PartCollection Id 为空或空白时，必须抛出 <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePartCollections_RejectsEmptyOrWhitespaceCollectionId(string id)
    {
        var collections = new[]
        {
            new FrontedV3PartCollectionDefinition(
                id: id,
                strategy: FrontedV3PartCollectionStrategy.ReadOnly,
                itemCapabilities: FrontedV3PartCapabilities.None,
                collectionGetter: _ => new List<object>(),
                itemKeySelector: _ => "k")
        };

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => FrontedV3PartDefinitionValidator.ValidatePartCollections(collections, DummyControlType));
        Assert.Contains("PartCollection Id", ex.Message, StringComparison.Ordinal);
        Assert.Contains("non-empty", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 10. Duplicate PartCollection Id
    // -------------------------------------------------------------------

    /// <summary>
    /// 同一控件内出现重复 PartCollection Id 时，必须抛出 <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Fact]
    public void ValidatePartCollections_RejectsDuplicateCollectionId()
    {
        var collections = new[]
        {
            CreateMinimalCollection("Cells"),
            CreateMinimalCollection("Cells")
        };

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => FrontedV3PartDefinitionValidator.ValidatePartCollections(collections, DummyControlType));
        Assert.Contains("duplicate PartCollection Id 'Cells'", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 11. FixedTemplate without EnsureTemplateItems
    // -------------------------------------------------------------------

    /// <summary>
    /// PartCollection 使用 <see cref="FrontedV3PartCollectionStrategy.FixedTemplate"/> 策略
    /// 但未配置 <see cref="FrontedV3PartCollectionDefinition.EnsureTemplateItems"/> 时，
    /// 必须抛出 <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Fact]
    public void ValidatePartCollections_RejectsFixedTemplateWithoutEnsureTemplateItems()
    {
        var collections = new[]
        {
            new FrontedV3PartCollectionDefinition(
                id: "Cells",
                strategy: FrontedV3PartCollectionStrategy.FixedTemplate,
                itemCapabilities: FrontedV3PartCapabilities.None,
                collectionGetter: _ => new List<object>(),
                itemKeySelector: _ => "k",
                ensureTemplateItems: null)
        };

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => FrontedV3PartDefinitionValidator.ValidatePartCollections(collections, DummyControlType));
        Assert.Contains("FixedTemplate", ex.Message, StringComparison.Ordinal);
        Assert.Contains("EnsureTemplateItems", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 12. Templates without ApplyTemplate
    // -------------------------------------------------------------------

    /// <summary>
    /// PartCollection 声明了具名 <see cref="FrontedV3PartCollectionDefinition.Templates"/>
    /// 但未配置 <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 时，
    /// 必须抛出 <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Fact]
    public void ValidatePartCollections_RejectsTemplatesWithoutApplyTemplate()
    {
        var collections = new[]
        {
            new FrontedV3PartCollectionDefinition(
                id: "Cells",
                strategy: FrontedV3PartCollectionStrategy.Dynamic,
                itemCapabilities: FrontedV3PartCapabilities.None,
                collectionGetter: _ => new List<object>(),
                itemKeySelector: _ => "k")
            {
                Templates = new[] { new FrontedV3LayoutTemplate("BO3", "BO3") },
                ApplyTemplate = null
            }
        };

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => FrontedV3PartDefinitionValidator.ValidatePartCollections(collections, DummyControlType));
        Assert.Contains("Templates", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ApplyTemplate", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 13. Cross-collection Id conflict
    // -------------------------------------------------------------------

    /// <summary>
    /// 同一控件内 Part Id 与 PartCollection Id 冲突时，必须抛出 <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    [Fact]
    public void Validate_RejectsCrossCollectionIdConflict()
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(id: "Shared", capabilities: FrontedV3PartCapabilities.None)
        };
        var collections = new[]
        {
            CreateMinimalCollection("Shared")
        };

        var ex = Assert.Throws<FrontedLayoutConfigException>(
            () => FrontedV3PartDefinitionValidator.Validate(parts, collections, DummyControlType));
        Assert.Contains("conflicts with an existing Part Id", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'Shared'", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 14. Valid full configuration passes
    // -------------------------------------------------------------------

    /// <summary>
    /// 合法的 Part 与 PartCollection 组合（Id 唯一、Capabilities/Storage 配对、
    /// FixedTemplate 配 EnsureTemplateItems、Templates 配 ApplyTemplate）必须通过校验。
    /// </summary>
    [Fact]
    public void Validate_AcceptsValidFullConfiguration()
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(
                id: "Logo",
                capabilities: FrontedV3PartCapabilities.Resize,
                widthStorage: FrontedV3Storage.ClrProperty("LogoWidth"),
                heightStorage: FrontedV3Storage.ClrProperty("LogoHeight"))
        };
        var collections = new[]
        {
            new FrontedV3PartCollectionDefinition(
                id: "Cells",
                strategy: FrontedV3PartCollectionStrategy.FixedTemplate,
                itemCapabilities: FrontedV3PartCapabilities.MoveAndResize,
                collectionGetter: _ => new List<object>(),
                itemKeySelector: _ => "k",
                ensureTemplateItems: _ => { })
            {
                Templates = new[] { new FrontedV3LayoutTemplate("BO3", "BO3") },
                ApplyTemplate = (_, _) => true
            }
        };

        var exception = Record.Exception(
            () => FrontedV3PartDefinitionValidator.Validate(parts, collections, DummyControlType));
        Assert.Null(exception);
    }

    // -------------------------------------------------------------------
    // 15. Empty lists pass
    // -------------------------------------------------------------------

    /// <summary>
    /// 空 Part 列表与空 PartCollection 列表必须通过校验，表示控件没有声明任何 Part。
    /// </summary>
    [Fact]
    public void Validate_AcceptsEmptyLists()
    {
        var exception = Record.Exception(
            () => FrontedV3PartDefinitionValidator.Validate(
                Array.Empty<FrontedV3PartDefinition>(),
                Array.Empty<FrontedV3PartCollectionDefinition>(),
                DummyControlType));
        Assert.Null(exception);
    }

    // -------------------------------------------------------------------
    // 16. Built-in BorderedImage-like Part passes (Resize only, no X/Y)
    // -------------------------------------------------------------------

    /// <summary>
    /// 模拟 BorderedImage 内层 Image 的合法配置：Resize 能力 + Width/Height Storage，无 X/Y Storage。
    /// 这种配置必须通过校验，验证内置控件路径不会被新校验器破坏。
    /// </summary>
    [Fact]
    public void ValidateParts_AcceptsBorderedImageLikeResizeOnlyPart()
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(
                id: "Image",
                capabilities: FrontedV3PartCapabilities.Resize,
                widthStorage: FrontedV3Storage.ClrProperty("ImageWidth"),
                heightStorage: FrontedV3Storage.ClrProperty("ImageHeight"))
        };

        var exception = Record.Exception(
            () => FrontedV3PartDefinitionValidator.ValidateParts(parts, DummyControlType));
        Assert.Null(exception);
    }

    // -------------------------------------------------------------------
    // 17. Built-in MapV2-like Part passes (MoveAndResize with all 4 Storage)
    // -------------------------------------------------------------------

    /// <summary>
    /// 模拟 MapV2Display 内部部件的合法配置：MoveAndResize 能力 + 全部 4 个 Storage。
    /// 这种配置必须通过校验，验证内置控件路径不会被新校验器破坏。
    /// </summary>
    [Fact]
    public void ValidateParts_AcceptsMapV2LikeMoveAndResizePart()
    {
        var parts = new[]
        {
            new FrontedV3PartDefinition(
                id: "TeamName",
                capabilities: FrontedV3PartCapabilities.MoveAndResize,
                widthStorage: FrontedV3Storage.ClrProperty("W"),
                heightStorage: FrontedV3Storage.ClrProperty("H"),
                xStorage: FrontedV3Storage.ClrProperty("X"),
                yStorage: FrontedV3Storage.ClrProperty("Y"))
        };

        var exception = Record.Exception(
            () => FrontedV3PartDefinitionValidator.ValidateParts(parts, DummyControlType));
        Assert.Null(exception);
    }

    private static FrontedV3PartCollectionDefinition CreateMinimalCollection(string id)
    {
        return new FrontedV3PartCollectionDefinition(
            id: id,
            strategy: FrontedV3PartCollectionStrategy.Dynamic,
            itemCapabilities: FrontedV3PartCapabilities.None,
            collectionGetter: _ => new List<object>(),
            itemKeySelector: _ => "k");
    }
}

/// <summary>
/// 用于 <see cref="FrontedV3PartDefinitionValidatorTest"/> 的占位控件类型，仅用于错误消息上下文。
/// </summary>
internal sealed class DummyValidatorControl;
