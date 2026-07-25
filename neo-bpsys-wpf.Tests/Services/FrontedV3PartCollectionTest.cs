using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.ViewModels.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Phase 4 SubTask 5.6 测试：覆盖 PartCollection 体系、MapV2 固定 Part 迁移、
/// GlobalScoreRow FixedTemplate 集合迁移与 Designer 去特化的全部场景。
/// </summary>
/// <remarks>
/// <para>
/// 这些测试验证 Phase 4 的核心契约：
/// <list type="bullet">
/// <item>MapV2 的 5 个固定 Part 通过 <see cref="BuiltInPartDefinitionResolver"/> 提供，继续使用现有 <c>InternalParts</c> JSON 字段。</item>
/// <item>MapV2 部件移动通过通用 <see cref="FixedPartGeometryTarget"/> 写入 <c>InternalParts</c> 项的 X/Y。</item>
/// <item>GlobalScoreRow 的 Cells 注册为 <see cref="FrontedV3PartCollectionStrategy.FixedTemplate"/> 策略的 PartCollection，拒绝任意增删。</item>
/// <item>FixedTemplate 策略补齐缺失模板 Cell，但保留已有 Cell 的字段。</item>
/// <item>Collection Item Key 唯一，<see cref="CollectionItemGeometryTarget"/> 的 Move/Resize round-trip。</item>
/// <item>Designer 不再保留 MapV2/GlobalScore 的几何专用特判成员。</item>
/// </list>
/// </para>
/// <para>
/// 这些测试不涉及 WPF 视觉树，无需 STA 线程；JSON 契约验证使用 <see cref="JsonSerializer"/> 默认选项，
/// 与 Phase 3 测试保持一致。
/// </para>
/// </remarks>
public class FrontedV3PartCollectionTest
{
    // -------------------------------------------------------------------
    // 1. MapV2PartsUseExistingInternalPartsJson
    // -------------------------------------------------------------------

    /// <summary>
    /// MapV2DisplayControlConfig 通过 <see cref="BuiltInPartDefinitionResolver"/> 获取的 Part 定义
    /// 必须包含 TeamName/MapCard/MapName/CampName/PickingBorder 5 个固定部件；
    /// 序列化 JSON 必须保留现有 <c>InternalParts</c> 根级字段（含 <c>Part</c>/<c>X</c>/<c>Y</c>/<c>Width</c>/<c>Height</c>）。
    /// </summary>
    [Fact]
    public void MapV2PartsUseExistingInternalPartsJson()
    {
        var config = new MapV2DisplayControlConfig
        {
            Left = 10,
            Top = 20,
            Width = 200,
            Height = 155,
            InternalParts =
            [
                new MapV2InternalPartLayoutConfig
                {
                    Part = MapV2InternalStylePart.TeamName,
                    X = 0, Y = 0, Width = 200, Height = 50
                },
                new MapV2InternalPartLayoutConfig
                {
                    Part = MapV2InternalStylePart.MapCard,
                    X = 5, Y = 55, Width = 190, Height = 60
                }
            ]
        };

        Assert.True(BuiltInPartDefinitionResolver.HasParts(config));

        var parts = BuiltInPartDefinitionResolver.GetParts(config);

        // 5 个固定部件全部存在
        Assert.Equal(5, parts.Count);
        var partIds = parts.Select(p => p.Id).ToArray();
        Assert.Contains(MapV2InternalStylePart.TeamName.ToString(), partIds);
        Assert.Contains(MapV2InternalStylePart.MapCard.ToString(), partIds);
        Assert.Contains(MapV2InternalStylePart.MapName.ToString(), partIds);
        Assert.Contains(MapV2InternalStylePart.CampName.ToString(), partIds);
        Assert.Contains(MapV2InternalStylePart.PickingBorder.ToString(), partIds);

        // 所有 Part 必须可移动+可缩放，并且通过 InternalParts 列表项的 CLR 属性读写
        foreach (var part in parts)
        {
            Assert.True(part.Capabilities.CanMove);
            Assert.True(part.Capabilities.CanResize);
            Assert.NotNull(part.XStorage);
            Assert.NotNull(part.YStorage);
            Assert.NotNull(part.WidthStorage);
            Assert.NotNull(part.HeightStorage);
        }

        // 序列化 JSON 必须保留 InternalParts 字段及子字段
        var json = JsonSerializer.Serialize(config);
        Assert.Contains("\"InternalParts\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Part\"", json, StringComparison.Ordinal);
        Assert.Contains("\"X\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Y\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Width\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Height\"", json, StringComparison.Ordinal);
        Assert.Contains("\"MapV2Display\"", json, StringComparison.Ordinal);

        // 不出现 Options 嵌套对象
        Assert.DoesNotContain("\"Options\"", json, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 2. MapV2PartMovementUsesGenericGeometryTarget
    // -------------------------------------------------------------------

    /// <summary>
    /// 通过通用 <see cref="FixedPartGeometryTarget"/> 移动 MapV2 的某个固定 Part 时，
    /// 必须写入 Config 的 <c>InternalParts</c> 列表中对应项的 X/Y 属性，不引入专用 geometry 实现。
    /// </summary>
    [Fact]
    public void MapV2PartMovementUsesGenericGeometryTarget()
    {
        var config = new MapV2DisplayControlConfig
        {
            Width = 200,
            Height = 155
        };

        var parts = BuiltInPartDefinitionResolver.GetParts(config);
        var teamNamePart = parts.First(p =>
            p.Id == MapV2InternalStylePart.TeamName.ToString());

        var originalTeamName = config.InternalParts.First(item =>
            item.Part == MapV2InternalStylePart.TeamName);
        var originalX = originalTeamName.X;
        var originalY = originalTeamName.Y;

        var target = new FixedPartGeometryTarget(teamNamePart, config);
        target.MoveTo(left: 42, top: 17);

        // 同一列表项的 X/Y 被更新
        Assert.Equal(42, originalTeamName.X);
        Assert.Equal(17, originalTeamName.Y);

        // 其他 Part 的 InternalParts 项不应被波及
        var mapCard = config.InternalParts.First(item =>
            item.Part == MapV2InternalStylePart.MapCard);
        Assert.NotEqual(42, mapCard.X);
        Assert.NotEqual(17, mapCard.Y);

        // 反向校验：原始 X/Y 不应等于新值（除非原始值恰好就是 42/17，这里通过 EnsureParts 默认值不可能）
        Assert.NotEqual(originalX, originalTeamName.X);
        Assert.NotEqual(originalY, originalTeamName.Y);
    }

    /// <summary>
    /// 通过通用 <see cref="FixedPartGeometryTarget"/> 缩放 MapV2 的某个固定 Part 时，
    /// 必须同时写入对应 <c>InternalParts</c> 项的 Width/Height 与 X/Y。
    /// </summary>
    [Fact]
    public void MapV2PartResizeUsesGenericGeometryTarget()
    {
        var config = new MapV2DisplayControlConfig
        {
            Width = 200,
            Height = 155
        };

        var parts = BuiltInPartDefinitionResolver.GetParts(config);
        var mapCardPart = parts.First(p =>
            p.Id == MapV2InternalStylePart.MapCard.ToString());

        var mapCardItem = config.InternalParts.First(item =>
            item.Part == MapV2InternalStylePart.MapCard);

        var target = new FixedPartGeometryTarget(mapCardPart, config);
        target.ResizeTo(left: 7, top: 8, width: 123, height: 99);

        Assert.Equal(7, mapCardItem.X);
        Assert.Equal(8, mapCardItem.Y);
        Assert.Equal(123, mapCardItem.Width);
        Assert.Equal(99, mapCardItem.Height);
    }

    // -------------------------------------------------------------------
    // 3. GlobalScoreCellsUseFixedTemplatePolicy
    // -------------------------------------------------------------------

    /// <summary>
    /// GlobalScoreRowControlConfig 的 Cells 集合通过
    /// <see cref="BuiltInPartCollectionDefinitionResolver"/> 获取的定义必须为
    /// <see cref="FrontedV3PartCollectionStrategy.FixedTemplate"/> 策略，
    /// 且 <see cref="FrontedV3PartCollectionStrategy.CanAdd"/>/<see cref="FrontedV3PartCollectionStrategy.CanDelete"/>
    /// 均为 false。
    /// </summary>
    [Fact]
    public void GlobalScoreCellsUseFixedTemplatePolicy()
    {
        var config = new GlobalScoreRowControlConfig();

        Assert.True(BuiltInPartCollectionDefinitionResolver.HasCollections(config));

        var collections = BuiltInPartCollectionDefinitionResolver.GetCollections(config);
        Assert.Single(collections);

        var collection = collections[0];
        Assert.Equal("Cells", collection.Id);
        Assert.Same(FrontedV3PartCollectionStrategy.FixedTemplate, collection.Strategy);
        Assert.False(collection.Strategy.CanAdd);
        Assert.False(collection.Strategy.CanDelete);
        Assert.True(collection.Strategy.IsTemplateDriven);
        Assert.True(collection.ItemCapabilities.CanMove);
        Assert.True(collection.ItemCapabilities.CanResize);
        Assert.NotNull(collection.EnsureTemplateItems);
    }

    /// <summary>
    /// 非 GlobalScoreRow 的 Config 不得返回 PartCollection 定义。
    /// </summary>
    [Fact]
    public void BuiltInPartCollectionDefinitionResolverReturnsEmptyForNonGlobalScore()
    {
        var config = new TextFrontedControlConfig();
        Assert.False(BuiltInPartCollectionDefinitionResolver.HasCollections(config));
        Assert.Empty(BuiltInPartCollectionDefinitionResolver.GetCollections(config));
    }

    /// <summary>
    /// <see cref="BuiltInPartCollectionDefinitionResolver.FindCollection"/> 按 Id 查找集合定义。
    /// </summary>
    [Fact]
    public void BuiltInPartCollectionDefinitionResolverFindCollectionById()
    {
        var config = new GlobalScoreRowControlConfig();

        var found = BuiltInPartCollectionDefinitionResolver.FindCollection(config, "Cells");
        Assert.NotNull(found);
        Assert.Equal("Cells", found!.Id);

        var notFound = BuiltInPartCollectionDefinitionResolver.FindCollection(config, "NonExistent");
        Assert.Null(notFound);
    }

    // -------------------------------------------------------------------
    // 4. FixedTemplateRejectsAddAndDelete
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3PartCollectionStrategy.FixedTemplate"/> 必须拒绝增删；
    /// <see cref="FrontedV3PartCollectionStrategy.Dynamic"/> 必须允许增删；
    /// <see cref="FrontedV3PartCollectionStrategy.ReadOnly"/> 必须拒绝增删。
    /// </summary>
    [Fact]
    public void FixedTemplateRejectsAddAndDelete()
    {
        var fixedTemplate = FrontedV3PartCollectionStrategy.FixedTemplate;
        Assert.False(fixedTemplate.CanAdd);
        Assert.False(fixedTemplate.CanDelete);
        Assert.True(fixedTemplate.IsTemplateDriven);

        var dynamic = FrontedV3PartCollectionStrategy.Dynamic;
        Assert.True(dynamic.CanAdd);
        Assert.True(dynamic.CanDelete);
        Assert.False(dynamic.IsTemplateDriven);

        var readOnly = FrontedV3PartCollectionStrategy.ReadOnly;
        Assert.False(readOnly.CanAdd);
        Assert.False(readOnly.CanDelete);
        Assert.False(readOnly.IsTemplateDriven);
    }

    // -------------------------------------------------------------------
    // 5. MissingTemplateCellsAreRestored
    // -------------------------------------------------------------------

    /// <summary>
    /// GlobalScoreRow 的 <c>EnsureTemplateItems</c> 回调必须补齐缺失的 BO5 模板 Cell；
    /// 已有 Cell 的字段（Id、坐标、尺寸）必须保留。
    /// </summary>
    [Fact]
    public void MissingTemplateCellsAreRestored()
    {
        var config = new GlobalScoreRowControlConfig
        {
            MajorGameGap = 180,
            HalfGameGap = 90
        };

        // 手动放入一个已有 Cell（带自定义坐标/尺寸，验证补齐不覆盖已有字段）
        config.Cells.Add(new GlobalScoreCellConfig
        {
            Id = "Game1FirstHalf",
            GameNumber = 1,
            GameKind = ScoreGameKind.Normal,
            HalfKind = ScoreHalfKind.FirstHalf,
            X = 11,
            Y = 22,
            Width = 33,
            Height = 44
        });

        var collection = BuiltInPartCollectionDefinitionResolver
            .GetCollections(config)
            .First(c => c.Id == "Cells");

        Assert.NotNull(collection.EnsureTemplateItems);
        collection.EnsureTemplateItems!(config);

        // BO5 模板要求 12 个 Cell
        Assert.Equal(12, config.Cells.Count);

        // 已有 Cell 的字段必须保留
        var existing = config.Cells.First(c => c.Id == "Game1FirstHalf");
        Assert.Equal(11, existing.X);
        Assert.Equal(22, existing.Y);
        Assert.Equal(33, existing.Width);
        Assert.Equal(44, existing.Height);

        // 缺失的 BO5 模板 Cell 必须被补齐；通过与完整 BO5 模板对比 ID 集合
        var expectedIds = GlobalScoreRowCellLayoutHelper
            .CreateCompleteCellTemplate(isBo3Mode: false)
            .Select(c => c.Id)
            .ToHashSet(StringComparer.Ordinal);
        var actualIds = config.Cells.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        Assert.True(expectedIds.SetEquals(actualIds));
    }

    /// <summary>
    /// 已经完整的 BO5 模板行再次调用 <c>EnsureTemplateItems</c> 不得丢失或重复 Cell。
    /// </summary>
    [Fact]
    public void EnsureTemplateItemsIsIdempotentOnCompleteRow()
    {
        var config = new GlobalScoreRowControlConfig();
        config.Cells = GlobalScoreRowCellLayoutHelper.CreateCompleteCellTemplate(isBo3Mode: false);

        var collection = BuiltInPartCollectionDefinitionResolver
            .GetCollections(config)
            .First(c => c.Id == "Cells");

        collection.EnsureTemplateItems!(config);

        Assert.Equal(12, config.Cells.Count);
        // 所有 Id 唯一
        var ids = config.Cells.Select(c => c.Id).ToList();
        Assert.Equal(12, ids.Distinct(StringComparer.Ordinal).Count());
    }

    // -------------------------------------------------------------------
    // 6. CollectionItemKeyMustBeUnique
    // -------------------------------------------------------------------

    /// <summary>
    /// GlobalScoreRow 的 <c>ItemKeySelector</c> 必须以 <c>Cell.Id</c> 作为唯一键，
    /// 模板补齐后所有 Cell 的 Id 在集合内唯一。
    /// </summary>
    [Fact]
    public void CollectionItemKeyMustBeUnique()
    {
        var config = new GlobalScoreRowControlConfig();
        config.Cells = GlobalScoreRowCellLayoutHelper.CreateCompleteCellTemplate(isBo3Mode: false);

        var collection = BuiltInPartCollectionDefinitionResolver
            .GetCollections(config)
            .First(c => c.Id == "Cells");

        // ItemKeySelector 返回 Cell.Id
        var keys = config.Cells.Select(cell => collection.ItemKeySelector(cell)).ToList();
        Assert.Equal(config.Cells.Count, keys.Distinct(StringComparer.Ordinal).Count());

        // ItemKeySelector 返回的 key 必须与 Cell.Id 一致
        foreach (var cell in config.Cells)
        {
            Assert.Equal(cell.Id, collection.ItemKeySelector(cell));
        }
    }

    // -------------------------------------------------------------------
    // 7. CollectionItemMoveResizeRoundTrips
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="CollectionItemGeometryTarget"/> 的 Move/Resize 必须写入对应 Cell 的
    /// X/Y/Width/Height；JSON 序列化-反序列化往返后值必须保持。
    /// </summary>
    [Fact]
    public void CollectionItemMoveResizeRoundTrips()
    {
        var config = new GlobalScoreRowControlConfig();
        config.Cells = GlobalScoreRowCellLayoutHelper.CreateCompleteCellTemplate(isBo3Mode: false);

        var collection = BuiltInPartCollectionDefinitionResolver
            .GetCollections(config)
            .First(c => c.Id == "Cells");

        var firstCell = config.Cells[0];
        var itemKey = collection.ItemKeySelector(firstCell);

        var target = new CollectionItemGeometryTarget(collection, config, itemKey);

        // Move
        target.MoveTo(left: 100, top: 50);
        Assert.Equal(100, firstCell.X);
        Assert.Equal(50, firstCell.Y);

        // Resize
        target.ResizeTo(left: 100, top: 50, width: 80, height: 40);
        Assert.Equal(80, firstCell.Width);
        Assert.Equal(40, firstCell.Height);
        // MoveAndResize 能力下 ResizeTo 同时写入 X/Y
        Assert.Equal(100, firstCell.X);
        Assert.Equal(50, firstCell.Y);

        // JSON round-trip
        var json = JsonSerializer.Serialize(config);
        var deserialized = JsonSerializer.Deserialize<GlobalScoreRowControlConfig>(json);
        Assert.NotNull(deserialized);
        var deserializedCell = deserialized!.Cells.First(c =>
            string.Equals(c.Id, itemKey, StringComparison.Ordinal));
        Assert.Equal(100, deserializedCell.X);
        Assert.Equal(50, deserializedCell.Y);
        Assert.Equal(80, deserializedCell.Width);
        Assert.Equal(40, deserializedCell.Height);
    }

    /// <summary>
    /// <see cref="CollectionItemGeometryTarget"/> 的几何读取属性
    /// <see cref="CollectionItemGeometryTarget.Left"/>/<see cref="CollectionItemGeometryTarget.Top"/>/
    /// <see cref="CollectionItemGeometryTarget.Width"/>/<see cref="CollectionItemGeometryTarget.Height"/>
    /// 必须返回当前 Cell 的几何值。
    /// </summary>
    [Fact]
    public void CollectionItemGeometryTargetReadsCurrentValues()
    {
        var config = new GlobalScoreRowControlConfig();
        config.Cells = GlobalScoreRowCellLayoutHelper.CreateCompleteCellTemplate(isBo3Mode: false);

        var collection = BuiltInPartCollectionDefinitionResolver
            .GetCollections(config)
            .First(c => c.Id == "Cells");

        var firstCell = config.Cells[0];
        var itemKey = collection.ItemKeySelector(firstCell);

        var target = new CollectionItemGeometryTarget(collection, config, itemKey);

        Assert.Equal(firstCell.X, target.Left);
        Assert.Equal(firstCell.Y, target.Top);
        Assert.Equal(firstCell.Width, target.Width);
        Assert.Equal(firstCell.Height, target.Height);

        // 修改 Cell 后再读取，证明读取的是当前 Config 值而非缓存
        firstCell.X = 999;
        firstCell.Width = 222;
        Assert.Equal(999, target.Left);
        Assert.Equal(222, target.Width);
    }

    /// <summary>
    /// <see cref="CollectionItemGeometryTarget"/> 构造函数拒绝 null 参数。
    /// </summary>
    [Fact]
    public void CollectionItemGeometryTargetRejectsNullArguments()
    {
        var config = new GlobalScoreRowControlConfig();
        var collection = BuiltInPartCollectionDefinitionResolver
            .GetCollections(config)
            .First(c => c.Id == "Cells");

        Assert.Throws<ArgumentNullException>(() =>
            new CollectionItemGeometryTarget(null!, config, "Game1FirstHalf"));
        Assert.Throws<ArgumentNullException>(() =>
            new CollectionItemGeometryTarget(collection, null!, "Game1FirstHalf"));
        Assert.Throws<ArgumentNullException>(() =>
            new CollectionItemGeometryTarget(collection, config, null!));
    }

    /// <summary>
    /// <see cref="CollectionItemGeometryTarget.ApplyToVisual"/> 触发视觉同步回调。
    /// </summary>
    [Fact]
    public void CollectionItemGeometryTargetApplyToVisualInvokesCallback()
    {
        var config = new GlobalScoreRowControlConfig();
        config.Cells = GlobalScoreRowCellLayoutHelper.CreateCompleteCellTemplate(isBo3Mode: false);

        var collection = BuiltInPartCollectionDefinitionResolver
            .GetCollections(config)
            .First(c => c.Id == "Cells");

        var firstCell = config.Cells[0];
        var itemKey = collection.ItemKeySelector(firstCell);

        var invoked = false;
        var target = new CollectionItemGeometryTarget(collection, config, itemKey, () => invoked = true);

        target.ApplyToVisual();
        Assert.True(invoked);
    }

    /// <summary>
    /// <see cref="FrontedV3PartCollectionPropertyContext"/> 携带集合定义、Config 与项键。
    /// </summary>
    [Fact]
    public void PartCollectionPropertyContextHoldsDefinitionAndConfig()
    {
        var config = new GlobalScoreRowControlConfig();
        var collection = BuiltInPartCollectionDefinitionResolver
            .GetCollections(config)
            .First(c => c.Id == "Cells");

        var context = new FrontedV3PartCollectionPropertyContext(collection, config, "Game1FirstHalf");

        Assert.Same(collection, context.CollectionDefinition);
        Assert.Same(config, context.Config);
        Assert.Equal("Game1FirstHalf", context.ItemKey);
    }

    /// <summary>
    /// <see cref="FrontedV3PartCollectionPropertyContext"/> 构造函数拒绝 null 参数。
    /// </summary>
    [Fact]
    public void PartCollectionPropertyContextRejectsNullArguments()
    {
        var config = new GlobalScoreRowControlConfig();
        var collection = BuiltInPartCollectionDefinitionResolver
            .GetCollections(config)
            .First(c => c.Id == "Cells");

        Assert.Throws<ArgumentNullException>(() =>
            new FrontedV3PartCollectionPropertyContext(null!, config, "Game1FirstHalf"));
        Assert.Throws<ArgumentNullException>(() =>
            new FrontedV3PartCollectionPropertyContext(collection, null!, "Game1FirstHalf"));
        Assert.Throws<ArgumentNullException>(() =>
            new FrontedV3PartCollectionPropertyContext(collection, config, null!));
    }

    // -------------------------------------------------------------------
    // 8. DesignerHasNoMapV2OrGlobalScoreGeometrySpecialCases
    // -------------------------------------------------------------------

    /// <summary>
    /// Designer ViewModel 不得包含 MapV2/GlobalScore 几何专用特判成员：
    /// <c>SelectedMapV2InternalStylePart</c>、<c>MoveSelectedMapV2InternalPart</c>、
    /// <c>ResizeSelectedMapV2InternalPart</c>、<c>SelectedGlobalScoreCell</c>、
    /// <c>HasGlobalScoreCellEditor</c>、<c>MoveSelectedGlobalScoreCell</c>、
    /// <c>ResizeSelectedGlobalScoreCell</c>。
    /// </summary>
    /// <remarks>
    /// Phase 4 只删除几何特判；样式转移相关的 MapV2/GlobalScore 引用属于 Phase 5 范围，
    /// 不在本次扫描范围。该测试只覆盖 Phase 4 列出的几何特判成员。
    /// </remarks>
    [Fact]
    public void DesignerHasNoMapV2OrGlobalScoreGeometrySpecialCases()
    {
        var viewModelType = typeof(FrontedDesignerWindowViewModel);
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // 不得包含这些控件专用 selection 属性
        Assert.Null(viewModelType.GetProperty("SelectedMapV2InternalStylePart", flags));
        Assert.Null(viewModelType.GetProperty("SelectedGlobalScoreCell", flags));
        Assert.Null(viewModelType.GetProperty("HasGlobalScoreCellEditor", flags));

        // 不得包含这些控件专用 Move/Resize 方法
        Assert.Null(viewModelType.GetMethod("MoveSelectedMapV2InternalPart", flags));
        Assert.Null(viewModelType.GetMethod("ResizeSelectedMapV2InternalPart", flags));
        Assert.Null(viewModelType.GetMethod("MoveSelectedGlobalScoreCell", flags));
        Assert.Null(viewModelType.GetMethod("ResizeSelectedGlobalScoreCell", flags));
    }

    /// <summary>
    /// <see cref="BuiltInPartDefinitionResolver"/> 与
    /// <see cref="BuiltInPartCollectionDefinitionResolver"/> 必须为 MapV2/GlobalScore
    /// 正确提供 Part/PartCollection 定义，证明 Designer 可以通过通用 API 而非特判代码处理这两个控件。
    /// </summary>
    [Fact]
    public void ResolversProvideDefinitionsForMapV2AndGlobalScore()
    {
        var mapV2Config = new MapV2DisplayControlConfig();
        Assert.True(BuiltInPartDefinitionResolver.HasParts(mapV2Config));
        var mapV2Parts = BuiltInPartDefinitionResolver.GetParts(mapV2Config);
        Assert.Equal(5, mapV2Parts.Count);

        var globalScoreConfig = new GlobalScoreRowControlConfig();
        Assert.True(BuiltInPartCollectionDefinitionResolver.HasCollections(globalScoreConfig));
        var collections = BuiltInPartCollectionDefinitionResolver.GetCollections(globalScoreConfig);
        Assert.Single(collections);
        Assert.Equal("Cells", collections[0].Id);
    }
}
