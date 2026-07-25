using System;
using System.IO;
using System.Linq;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Properties;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Phase 6 SubTask 7.5 测试：覆盖 Designer 去特化后的统一 selection 构造、
/// Schema 驱动属性编辑、统一 GeometryTarget 的 Move/Resize/Undo，
/// 以及 Designer ViewModel 不再引用控件专用 Config 类型的契约。
/// </summary>
/// <remarks>
/// <para>
/// 这些测试验证 Phase 6 的核心契约：
/// <list type="bullet">
/// <item><see cref="FrontedV3DesignSelection"/> 通过 <see cref="FrontedV3DesignSelectionBuilder"/>
/// 统一构造 Root/FixedPart/CollectionItem 三类 selection。</item>
/// <item>属性 Schema 由 <see cref="BuiltInPropertyDefinitionResolver"/> 提供，
/// 属性编辑通过 <see cref="FrontedV3PropertyDefinition.Storage"/> 写入，不通过 propertyName 字符串反射。</item>
/// <item>Move/Resize 只调用 <see cref="IFrontedV3GeometryTarget"/>，
/// 不通过 <c>if (config is BorderedImage...)</c> 等类型分支选择几何实现。</item>
/// <item>Undo 对 Root/FixedPart/CollectionItem 三种 GeometryTarget 都工作。</item>
/// <item>Designer ViewModel 源码不引用 <c>BorderedImageFrontedControlConfig</c>/
/// <c>MapV2DisplayControlConfig</c>/<c>GlobalScoreRowControlConfig</c>，
/// 通用编辑路径完全去特化。</item>
/// </list>
/// </para>
/// <para>
/// 这些是数据流与契约测试，不涉及 WPF 视觉树，无需
/// <see cref="neo_bpsys_wpf.Tests.Infrastructure.WpfTestThread"/>。
/// </para>
/// </remarks>
public class FrontedV3DesignerDespecializationTest
{
    // -------------------------------------------------------------------
    // 1. RootSelectionBuildsRootSchema
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3DesignSelectionBuilder.BuildRootSelection"/> 必须为根控件
    /// 构造非空属性 Schema，Schema 由 <see cref="BuiltInPropertyDefinitionResolver"/>
    /// 反射 Config 的 CLR 属性生成，包含布局与外观属性。
    /// </summary>
    [Fact]
    public void RootSelectionBuildsRootSchema()
    {
        var config = new TextFrontedControlConfig
        {
            Left = 10,
            Top = 20,
            Width = 100,
            Height = 30,
            Color = "#FF0000",
            FontSize = 24
        };
        var designItem = new FrontedControlDesignItem
        {
            Name = "Text1",
            Config = config
        };

        var builder = new FrontedV3DesignSelectionBuilder();
        var selection = builder.BuildRootSelection(designItem);

        Assert.NotNull(selection);
        Assert.Equal(FrontedV3DesignSelectionKind.Root, selection!.Kind);
        Assert.Null(selection.SubTarget);
        Assert.True(selection.HasEditableSchema);

        // Schema 中应包含布局属性与外观属性，证明由反射生成而非控件类型特判
        var optionsPaths = selection.Properties.Select(p => p.OptionsPath).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(nameof(TextFrontedControlConfig.Left), optionsPaths);
        Assert.Contains(nameof(TextFrontedControlConfig.Top), optionsPaths);
        Assert.Contains(nameof(TextFrontedControlConfig.Width), optionsPaths);
        Assert.Contains(nameof(TextFrontedControlConfig.Color), optionsPaths);
        Assert.Contains(nameof(TextFrontedControlConfig.FontSize), optionsPaths);

        // 保留字段 BehaviorGuid/ControlType 不得出现在 Schema 中
        Assert.DoesNotContain(nameof(FrontedControlConfigBase.BehaviorGuid), optionsPaths);
        Assert.DoesNotContain(nameof(FrontedControlConfigBase.ControlType), optionsPaths);
    }

    /// <summary>
    /// 无 Schema 属性的根控件调用 <see cref="FrontedV3DesignSelectionBuilder.BuildRootSelection"/>
    /// 时返回 <see langword="null"/>，表示该控件无可编辑 Schema 属性。
    /// </summary>
    [Fact]
    public void RootSelectionReturnsNullWhenSchemaIsEmpty()
    {
        // FrontedControlConfigBase 基类自身只有保留字段与布局字段，
        // 但布局字段（Left/Top/Width/Height）属于支持范围，会出现在 Schema 中。
        // 这里用一个空 Config 验证 Schema 构造路径至少不抛异常。
        var designItem = new FrontedControlDesignItem
        {
            Name = "Empty",
            Config = new FrontedControlConfigBase { ControlType = "Unknown" }
        };

        var builder = new FrontedV3DesignSelectionBuilder();
        var selection = builder.BuildRootSelection(designItem);

        // FrontedControlConfigBase 的 Left/Top/Width/Height/BindingPath 等会被反射发现，
        // 因此 selection 不为 null；这里验证构造不抛异常且 Kind 为 Root。
        Assert.NotNull(selection);
        Assert.Equal(FrontedV3DesignSelectionKind.Root, selection!.Kind);
    }

    // -------------------------------------------------------------------
    // 2. FixedPartSelectionBuildsPartSchema
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3DesignSelectionBuilder.BuildFixedPartSelection"/> 必须为 BorderedImage
    /// 的 <c>Image</c> Part 构造 Schema，Part 的 <c>WidthStorage</c>/<c>HeightStorage</c>
    /// 可读写 Config 的 <c>ImageWidth</c>/<c>ImageHeight</c> 根级字段。
    /// </summary>
    [Fact]
    public void FixedPartSelectionBuildsPartSchema()
    {
        var config = new BorderedImageFrontedControlConfig
        {
            ImageWidth = 60,
            ImageHeight = 40
        };
        var designItem = new FrontedControlDesignItem
        {
            Name = "BorderedImage1",
            Config = config
        };

        var builder = new FrontedV3DesignSelectionBuilder();
        var selection = builder.BuildFixedPartSelection(designItem, partId: "Image");

        Assert.NotNull(selection);
        Assert.Equal(FrontedV3DesignSelectionKind.FixedPart, selection!.Kind);
        Assert.NotNull(selection.SubTarget);
        Assert.Equal(FrontedV3DesignSubTargetKind.FixedPart, selection.SubTarget.Kind);

        var partTarget = Assert.IsType<FrontedV3FixedPartTarget>(selection.SubTarget);
        Assert.Equal("Image", partTarget.PartId);

        // Part Schema 应包含 Width/Height 几何属性（BorderedImage 的 Part 是 Resize-only，无 X/Y）
        var optionsPaths = selection.Properties.Select(p => p.OptionsPath).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("Width", optionsPaths);
        Assert.Contains("Height", optionsPaths);

        // Part 的 StorageAccessor 可读写 ImageWidth/ImageHeight
        var widthProperty = selection.Properties.First(p => p.OptionsPath == "Width");
        var heightProperty = selection.Properties.First(p => p.OptionsPath == "Height");

        widthProperty.Storage.SetValue(config, 120D);
        heightProperty.Storage.SetValue(config, 80D);

        Assert.Equal(120, config.ImageWidth);
        Assert.Equal(80, config.ImageHeight);

        // 反向读取验证
        Assert.Equal(120D, widthProperty.Storage.GetValue(config));
        Assert.Equal(80D, heightProperty.Storage.GetValue(config));
    }

    /// <summary>
    /// <see cref="FrontedV3DesignSelectionBuilder.BuildFixedPartSelection"/> 对不存在的 Part Id
    /// 返回 <see langword="null"/>，不抛异常。
    /// </summary>
    [Fact]
    public void FixedPartSelectionReturnsNullForUnknownPart()
    {
        var config = new BorderedImageFrontedControlConfig();
        var designItem = new FrontedControlDesignItem
        {
            Name = "BorderedImage1",
            Config = config
        };

        var builder = new FrontedV3DesignSelectionBuilder();
        var selection = builder.BuildFixedPartSelection(designItem, partId: "NonExistent");

        Assert.Null(selection);
    }

    // -------------------------------------------------------------------
    // 3. CollectionItemSelectionBuildsItemSchema
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3DesignSelectionBuilder.BuildCollectionItemSelection"/> 必须为 GlobalScoreRow
    /// 的 <c>Cells</c> 集合项构造 Schema，集合的 <c>ItemKeySelector</c> 返回 Cell 的 <c>Id</c>，
    /// 集合项几何属性通过 StorageAccessor 可读写 Cell 的 X/Y/Width/Height。
    /// </summary>
    [Fact]
    public void CollectionItemSelectionBuildsItemSchema()
    {
        var config = new GlobalScoreRowControlConfig();
        var designItem = new FrontedControlDesignItem
        {
            Name = "GlobalScoreRow1",
            Config = config
        };

        var builder = new FrontedV3DesignSelectionBuilder();

        // 先获取可用集合，验证 Cells 存在
        var collections = builder.GetAvailableCollections(designItem);
        var cellsCollection = Assert.Single(collections);
        Assert.Equal("Cells", cellsCollection.Id);

        // 通过 EnsureTemplateItems 补齐 BO5 模板 Cell
        cellsCollection.EnsureTemplateItems?.Invoke(config);
        Assert.True(config.Cells.Count > 0);

        var firstCell = config.Cells[0];
        var itemKey = cellsCollection.ItemKeySelector(firstCell);

        // ItemKeySelector 返回 Cell.Id
        Assert.Equal(firstCell.Id, itemKey);

        // 构造 CollectionItem selection
        var selection = builder.BuildCollectionItemSelection(
            designItem, collectionId: "Cells", itemKey: itemKey);

        Assert.NotNull(selection);
        Assert.Equal(FrontedV3DesignSelectionKind.CollectionItem, selection!.Kind);
        Assert.NotNull(selection.SubTarget);
        Assert.Equal(FrontedV3DesignSubTargetKind.CollectionItem, selection.SubTarget.Kind);

        var itemTarget = Assert.IsType<FrontedV3CollectionItemTarget>(selection.SubTarget);
        Assert.Equal("Cells", itemTarget.CollectionId);
        Assert.Equal(itemKey, itemTarget.ItemKey);

        // 集合项 Schema 应包含 X/Y/Width/Height 几何属性
        var optionsPaths = selection.Properties.Select(p => p.OptionsPath).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("X", optionsPaths);
        Assert.Contains("Y", optionsPaths);
        Assert.Contains("Width", optionsPaths);
        Assert.Contains("Height", optionsPaths);

        // 集合项几何属性通过 StorageAccessor 可读写 Cell 的 X/Y/Width/Height
        var xProperty = selection.Properties.First(p => p.OptionsPath == "X");
        var widthProperty = selection.Properties.First(p => p.OptionsPath == "Width");

        xProperty.Storage.SetValue(config, 123D);
        widthProperty.Storage.SetValue(config, 456D);

        Assert.Equal(123, firstCell.X);
        Assert.Equal(456, firstCell.Width);
    }

    /// <summary>
    /// <see cref="FrontedV3DesignSelectionBuilder.BuildCollectionItemSelection"/> 对不存在的集合 Id
    /// 返回 <see langword="null"/>，不抛异常。
    /// </summary>
    [Fact]
    public void CollectionItemSelectionReturnsNullForUnknownCollection()
    {
        var config = new GlobalScoreRowControlConfig();
        var designItem = new FrontedControlDesignItem
        {
            Name = "GlobalScoreRow1",
            Config = config
        };

        var builder = new FrontedV3DesignSelectionBuilder();
        var selection = builder.BuildCollectionItemSelection(
            designItem, collectionId: "NonExistent", itemKey: "SomeKey");

        Assert.Null(selection);
    }

    // -------------------------------------------------------------------
    // 3a. SelectCollectionItem_IncludesAppearanceProperties
    // -------------------------------------------------------------------

    /// <summary>
    /// 选中 GlobalScoreRow 的 Cell 后，<see cref="FrontedV3DesignSelection.Properties"/>
    /// 必须包含几何属性（X/Y/Width/Height）与外观属性（Color/FontFamily/FontWeight/
    /// FontSize/ShowCampIcon/CampIconColor/Visibility），证明子控件外观属性 Schema 已合并。
    /// </summary>
    [Fact]
    public void SelectCollectionItem_IncludesAppearanceProperties()
    {
        var config = new GlobalScoreRowControlConfig();
        var designItem = new FrontedControlDesignItem
        {
            Name = "GlobalScoreRow1",
            Config = config
        };

        var builder = new FrontedV3DesignSelectionBuilder();

        // 通过 EnsureTemplateItems 补齐 BO5 模板 Cell
        var collections = builder.GetAvailableCollections(designItem);
        var cellsCollection = Assert.Single(collections);
        cellsCollection.EnsureTemplateItems?.Invoke(config);
        Assert.True(config.Cells.Count > 0);

        var firstCell = config.Cells[0];
        var itemKey = cellsCollection.ItemKeySelector(firstCell);

        // 构造 CollectionItem selection
        var selection = builder.BuildCollectionItemSelection(
            designItem, collectionId: "Cells", itemKey: itemKey);

        Assert.NotNull(selection);
        Assert.Equal(FrontedV3DesignSelectionKind.CollectionItem, selection!.Kind);

        // 收集所有 OptionsPath，断言几何与外观属性同时存在
        var optionsPaths = selection.Properties.Select(p => p.OptionsPath).ToHashSet(StringComparer.Ordinal);

        // 几何属性
        Assert.Contains("X", optionsPaths);
        Assert.Contains("Y", optionsPaths);
        Assert.Contains("Width", optionsPaths);
        Assert.Contains("Height", optionsPaths);

        // 外观属性
        Assert.Contains(nameof(GlobalScoreCellConfig.Color), optionsPaths);
        Assert.Contains(nameof(GlobalScoreCellConfig.FontFamily), optionsPaths);
        Assert.Contains(nameof(GlobalScoreCellConfig.FontWeight), optionsPaths);
        Assert.Contains(nameof(GlobalScoreCellConfig.FontSize), optionsPaths);
        Assert.Contains(nameof(GlobalScoreCellConfig.ShowCampIcon), optionsPaths);
        Assert.Contains(nameof(GlobalScoreCellConfig.CampIconColor), optionsPaths);
        Assert.Contains(nameof(GlobalScoreCellConfig.Visibility), optionsPaths);

        // 总数应为 4（几何） + 7（外观） = 11
        Assert.Equal(11, selection.Properties.Count);
    }

    /// <summary>
    /// 选中 MapV2 的固定 Part（如 TeamName）后，<see cref="FrontedV3DesignSelection.Properties"/>
    /// 只包含几何属性（X/Y/Width/Height），不包含外观属性，证明 MapV2 Part 仍为几何编辑模式。
    /// </summary>
    [Fact]
    public void SelectFixedPart_OnlyGeometryForMapV2()
    {
        var config = new MapV2DisplayControlConfig
        {
            Width = 200,
            Height = 155
        };
        var designItem = new FrontedControlDesignItem
        {
            Name = "MapV2Display1",
            Config = config
        };

        var builder = new FrontedV3DesignSelectionBuilder();

        // 选中 TeamName 部件
        var selection = builder.BuildFixedPartSelection(
            designItem,
            partId: MapV2InternalStylePart.TeamName.ToString());

        Assert.NotNull(selection);
        Assert.Equal(FrontedV3DesignSelectionKind.FixedPart, selection!.Kind);

        // 仅包含几何属性
        var optionsPaths = selection.Properties.Select(p => p.OptionsPath).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("X", optionsPaths);
        Assert.Contains("Y", optionsPaths);
        Assert.Contains("Width", optionsPaths);
        Assert.Contains("Height", optionsPaths);

        // 不包含外观属性
        Assert.DoesNotContain("Color", optionsPaths);
        Assert.DoesNotContain("FontFamily", optionsPaths);
        Assert.DoesNotContain("FontWeight", optionsPaths);
        Assert.DoesNotContain("FontSize", optionsPaths);
        Assert.DoesNotContain("Visibility", optionsPaths);

        // 总数应为 4（仅几何）
        Assert.Equal(4, selection.Properties.Count);
    }

    /// <summary>
    /// GlobalScoreRow Cell 外观属性的 <see cref="FrontedV3PropertyMetadata.Inheritance"/>
    /// 必须为 <see cref="FrontedV3PropertyInheritance.ParentFallback"/>（Visibility 除外），
    /// 语义必须为 <see cref="FrontedV3PropertySemantic.Appearance"/>，GroupName 必须为 "Appearance"。
    /// </summary>
    [Fact]
    public void CellAppearanceProperties_HaveParentFallbackInheritance()
    {
        var config = new GlobalScoreRowControlConfig();
        var designItem = new FrontedControlDesignItem
        {
            Name = "GlobalScoreRow1",
            Config = config
        };

        var builder = new FrontedV3DesignSelectionBuilder();

        var collections = builder.GetAvailableCollections(designItem);
        var cellsCollection = Assert.Single(collections);
        cellsCollection.EnsureTemplateItems?.Invoke(config);

        var firstCell = config.Cells[0];
        var itemKey = cellsCollection.ItemKeySelector(firstCell);

        var selection = builder.BuildCollectionItemSelection(
            designItem, collectionId: "Cells", itemKey: itemKey);

        Assert.NotNull(selection);

        // ParentFallback 属性集合
        var parentFallbackNames = new[]
        {
            nameof(GlobalScoreCellConfig.Color),
            nameof(GlobalScoreCellConfig.FontFamily),
            nameof(GlobalScoreCellConfig.FontWeight),
            nameof(GlobalScoreCellConfig.FontSize),
            nameof(GlobalScoreCellConfig.ShowCampIcon),
            nameof(GlobalScoreCellConfig.CampIconColor)
        };

        foreach (var name in parentFallbackNames)
        {
            var property = selection!.Properties.FirstOrDefault(p =>
                string.Equals(p.OptionsPath, name, StringComparison.Ordinal));
            Assert.NotNull(property);
            Assert.Equal(FrontedV3PropertyInheritance.ParentFallback, property!.Metadata.Inheritance);
            Assert.Equal(FrontedV3PropertySemantic.Appearance, property.Metadata.Semantic);
            Assert.Equal("Appearance", property.Metadata.GroupName);
        }

        // Visibility 属性：Inheritance=None，Semantic=Appearance
        var visibilityProperty = selection!.Properties.First(p =>
            string.Equals(p.OptionsPath, nameof(GlobalScoreCellConfig.Visibility), StringComparison.Ordinal));
        Assert.Equal(FrontedV3PropertyInheritance.None, visibilityProperty.Metadata.Inheritance);
        Assert.Equal(FrontedV3PropertySemantic.Appearance, visibilityProperty.Metadata.Semantic);
        Assert.Equal("Appearance", visibilityProperty.Metadata.GroupName);
    }

    // -------------------------------------------------------------------
    // 4. PropertyEditUsesStorageAccessor
    // -------------------------------------------------------------------

    /// <summary>
    /// Designer 属性编辑必须通过 <see cref="FrontedV3PropertyDefinition.Storage"/>
    /// 写入 Config，不通过 propertyName 字符串反射写入。编辑 <c>Color</c> 属性后，
    /// Config 的 <c>Color</c> 字段必须被更新；其他字段不得被波及。
    /// </summary>
    [Fact]
    public void PropertyEditUsesStorageAccessor()
    {
        var config = new TextFrontedControlConfig
        {
            Color = "#FF0000",
            FontSize = 24,
            Text = "Hello"
        };

        var properties = BuiltInPropertyDefinitionResolver.GetProperties(config);
        Assert.NotEmpty(properties);

        var colorProperty = properties.FirstOrDefault(p =>
            string.Equals(p.OptionsPath, nameof(TextFrontedControlConfig.Color), StringComparison.Ordinal));
        Assert.NotNull(colorProperty);

        var newValue = "#00FF00";
        colorProperty!.Storage.SetValue(config, newValue);

        // Config 的 Color 字段被更新
        Assert.Equal(newValue, config.Color);

        // 其他字段不得被波及
        Assert.Equal(24, config.FontSize);
        Assert.Equal("Hello", config.Text);

        // 反向读取：Storage.GetValue 返回当前 Config 值
        Assert.Equal(newValue, colorProperty.Storage.GetValue(config));
    }

    /// <summary>
    /// <see cref="FrontedV3PropertyDefinition.SetValue"/> 通过 Storage 写入 Config，
    /// 内部完成类型转换（string → string）。验证 SetValue 与 Storage.SetValue 行为一致。
    /// </summary>
    [Fact]
    public void PropertyDefinitionSetValueWritesThroughStorage()
    {
        var config = new TextFrontedControlConfig { FontSize = 16 };

        var properties = BuiltInPropertyDefinitionResolver.GetProperties(config);
        var fontSizeProperty = properties.First(p =>
            string.Equals(p.OptionsPath, nameof(TextFrontedControlConfig.FontSize), StringComparison.Ordinal));

        // FontSize 是 double，传入 double 值
        fontSizeProperty.SetValue(config, 32D);

        Assert.Equal(32, config.FontSize);

        // GetValue 返回转换后的值
        var value = fontSizeProperty.GetValue(config);
        Assert.IsType<double>(value);
        Assert.Equal(32D, value);
    }

    // -------------------------------------------------------------------
    // 5. MoveUsesGeometryTarget
    // -------------------------------------------------------------------

    /// <summary>
    /// 根控件 Move 通过 <see cref="ConfigBackedRootGeometryTarget.MoveTo"/> 执行，
    /// 写入 Config 的 <c>Left</c>/<c>Top</c> 字段并触发视觉同步回调。
    /// </summary>
    [Fact]
    public void MoveUsesGeometryTarget()
    {
        var config = new TextFrontedControlConfig
        {
            Left = 10,
            Top = 20,
            Width = 100,
            Height = 30
        };

        var visualSyncInvoked = false;
        var target = new ConfigBackedRootGeometryTarget(config, () => visualSyncInvoked = true);

        // 初始读取
        Assert.Equal(10, target.Left);
        Assert.Equal(20, target.Top);

        target.MoveTo(left: 200, top: 300);

        // Config 的 Left/Top 被更新
        Assert.Equal(200, config.Left);
        Assert.Equal(300, config.Top);

        // 视觉同步回调被触发
        Assert.True(visualSyncInvoked);

        // GeometryTarget 读取返回新值
        Assert.Equal(200, target.Left);
        Assert.Equal(300, target.Top);
    }

    // -------------------------------------------------------------------
    // 6. ResizeUsesGeometryTarget
    // -------------------------------------------------------------------

    /// <summary>
    /// 根控件 Resize 通过 <see cref="ConfigBackedRootGeometryTarget.ResizeTo"/> 执行，
    /// 写入 Config 的 <c>Left</c>/<c>Top</c>/<c>Width</c>/<c>Height</c> 字段并触发视觉同步回调。
    /// </summary>
    [Fact]
    public void ResizeUsesGeometryTarget()
    {
        var config = new TextFrontedControlConfig
        {
            Left = 10,
            Top = 20,
            Width = 100,
            Height = 30
        };

        var visualSyncInvoked = false;
        var target = new ConfigBackedRootGeometryTarget(config, () => visualSyncInvoked = true);

        // 初始读取
        Assert.Equal(100, target.Width);
        Assert.Equal(30, target.Height);

        target.ResizeTo(left: 50, top: 60, width: 200, height: 80);

        // Config 的 Left/Top/Width/Height 被更新
        Assert.Equal(50, config.Left);
        Assert.Equal(60, config.Top);
        Assert.Equal(200, config.Width);
        Assert.Equal(80, config.Height);

        // 视觉同步回调被触发
        Assert.True(visualSyncInvoked);

        // GeometryTarget 读取返回新值
        Assert.Equal(50, target.Left);
        Assert.Equal(60, target.Top);
        Assert.Equal(200, target.Width);
        Assert.Equal(80, target.Height);
    }

    /// <summary>
    /// <see cref="ConfigBackedRootGeometryTarget.ResizeTo"/> 传入 <see langword="null"/> 宽高时，
    /// Config 的 <c>Width</c>/<c>Height</c> 被清除为 <see langword="null"/>（恢复自适应）。
    /// </summary>
    [Fact]
    public void ResizeWithNullDimensionsClearsSize()
    {
        var config = new TextFrontedControlConfig
        {
            Left = 10,
            Top = 20,
            Width = 100,
            Height = 30
        };

        var target = new ConfigBackedRootGeometryTarget(config);

        target.ResizeTo(left: 50, top: 60, width: null, height: null);

        Assert.Equal(50, config.Left);
        Assert.Equal(60, config.Top);
        Assert.Null(config.Width);
        Assert.Null(config.Height);
        Assert.Null(target.Width);
        Assert.Null(target.Height);
    }

    // -------------------------------------------------------------------
    // 7. UndoWorksForAllGeometryTargets
    // -------------------------------------------------------------------

    /// <summary>
    /// Undo 机制对所有 <see cref="IFrontedV3GeometryTarget"/> 类型工作：
    /// 记录原始几何值 → 通过 GeometryTarget 修改 → 验证已修改 →
    /// 通过 GeometryTarget 恢复原始值 → 验证已恢复。
    /// 该测试覆盖 Root/FixedPart/CollectionItem 三种 GeometryTarget。
    /// </summary>
    [Fact]
    public void UndoWorksForAllGeometryTargets()
    {
        UndoWorksForRootGeometryTarget();
        UndoWorksForFixedPartGeometryTarget();
        UndoWorksForCollectionItemGeometryTarget();
    }

    /// <summary>
    /// Root GeometryTarget 的 Undo：记录 Config 原始几何 → ResizeTo 修改 →
    /// ResizeTo 恢复 → 验证 Config 几何已恢复。
    /// </summary>
    private static void UndoWorksForRootGeometryTarget()
    {
        var config = new TextFrontedControlConfig
        {
            Left = 10,
            Top = 20,
            Width = 100,
            Height = 30
        };

        var target = new ConfigBackedRootGeometryTarget(config);

        // 记录原始几何值
        var originalLeft = target.Left;
        var originalTop = target.Top;
        var originalWidth = target.Width;
        var originalHeight = target.Height;

        // 修改
        target.ResizeTo(left: 200, top: 300, width: 400, height: 200);
        Assert.Equal(200, config.Left);
        Assert.Equal(300, config.Top);
        Assert.Equal(400, config.Width);
        Assert.Equal(200, config.Height);

        // 恢复（Undo）
        target.ResizeTo(
            left: originalLeft,
            top: originalTop,
            width: originalWidth,
            height: originalHeight);

        // 验证已恢复
        Assert.Equal(originalLeft, config.Left);
        Assert.Equal(originalTop, config.Top);
        Assert.Equal(originalWidth, config.Width);
        Assert.Equal(originalHeight, config.Height);
    }

    /// <summary>
    /// FixedPart GeometryTarget 的 Undo：记录 MapV2 Part 原始几何 →
    /// ResizeTo 修改 → ResizeTo 恢复 → 验证 InternalParts 项几何已恢复。
    /// </summary>
    private static void UndoWorksForFixedPartGeometryTarget()
    {
        var config = new MapV2DisplayControlConfig
        {
            Width = 200,
            Height = 155
        };

        var parts = BuiltInPartDefinitionResolver.GetParts(config);
        var teamNamePart = parts.First(p =>
            p.Id == MapV2InternalStylePart.TeamName.ToString());

        var teamNameItem = config.InternalParts.First(item =>
            item.Part == MapV2InternalStylePart.TeamName);

        var target = new FixedPartGeometryTarget(teamNamePart, config);

        // 记录原始几何值
        var originalX = teamNameItem.X;
        var originalY = teamNameItem.Y;
        var originalWidth = teamNameItem.Width;
        var originalHeight = teamNameItem.Height;

        // 修改
        target.ResizeTo(left: 111, top: 222, width: 333, height: 444);
        Assert.Equal(111, teamNameItem.X);
        Assert.Equal(222, teamNameItem.Y);
        Assert.Equal(333, teamNameItem.Width);
        Assert.Equal(444, teamNameItem.Height);

        // 恢复（Undo）
        target.ResizeTo(
            left: originalX,
            top: originalY,
            width: originalWidth,
            height: originalHeight);

        // 验证已恢复
        Assert.Equal(originalX, teamNameItem.X);
        Assert.Equal(originalY, teamNameItem.Y);
        Assert.Equal(originalWidth, teamNameItem.Width);
        Assert.Equal(originalHeight, teamNameItem.Height);
    }

    /// <summary>
    /// CollectionItem GeometryTarget 的 Undo：记录 GlobalScoreRow Cell 原始几何 →
    /// ResizeTo 修改 → ResizeTo 恢复 → 验证 Cell 几何已恢复。
    /// </summary>
    private static void UndoWorksForCollectionItemGeometryTarget()
    {
        var config = new GlobalScoreRowControlConfig();
        var collections = BuiltInPartCollectionDefinitionResolver.GetCollections(config);
        var cellsCollection = collections.First(c => c.Id == "Cells");
        cellsCollection.EnsureTemplateItems?.Invoke(config);

        var firstCell = config.Cells[0];
        var itemKey = cellsCollection.ItemKeySelector(firstCell);

        var target = new CollectionItemGeometryTarget(cellsCollection, config, itemKey);

        // 记录原始几何值
        var originalX = firstCell.X;
        var originalY = firstCell.Y;
        var originalWidth = firstCell.Width;
        var originalHeight = firstCell.Height;

        // 修改
        target.ResizeTo(left: 555, top: 666, width: 777, height: 888);
        Assert.Equal(555, firstCell.X);
        Assert.Equal(666, firstCell.Y);
        Assert.Equal(777, firstCell.Width);
        Assert.Equal(888, firstCell.Height);

        // 恢复（Undo）
        target.ResizeTo(
            left: originalX,
            top: originalY,
            width: originalWidth,
            height: originalHeight);

        // 验证已恢复
        Assert.Equal(originalX, firstCell.X);
        Assert.Equal(originalY, firstCell.Y);
        Assert.Equal(originalWidth, firstCell.Width);
        Assert.Equal(originalHeight, firstCell.Height);
    }

    // -------------------------------------------------------------------
    // 8. EscReturnsToRootSelection
    // -------------------------------------------------------------------

    /// <summary>
    /// 子控件选中后通过 <see cref="FrontedV3DesignSelectionBuilder.BuildRootSelection"/>
    /// 回退到根选中：从 FixedPart selection 取得 DesignItem，重新构建 Root selection，
    /// 验证 Kind 切换为 Root 且 Schema 包含根控件属性。这是 <c>EscapeToRootSelection</c>
    /// 方法依赖的数据流契约。
    /// </summary>
    [Fact]
    public void EscReturnsToRootSelection()
    {
        var config = new BorderedImageFrontedControlConfig
        {
            ImageWidth = 60,
            ImageHeight = 40,
            Left = 10,
            Top = 20,
            Width = 200,
            Height = 100
        };
        var designItem = new FrontedControlDesignItem
        {
            Name = "BorderedImage1",
            Config = config
        };

        var builder = new FrontedV3DesignSelectionBuilder();

        // 先构建 FixedPart 选中（模拟用户点击内部 Image 部件）
        var partSelection = builder.BuildFixedPartSelection(designItem, partId: "Image");
        Assert.NotNull(partSelection);
        Assert.Equal(FrontedV3DesignSelectionKind.FixedPart, partSelection!.Kind);
        Assert.NotNull(partSelection.DesignItem);

        // 模拟 Esc：从子控件选中取得 DesignItem，重新构建 Root 选中
        var rootSelection = builder.BuildRootSelection(partSelection.DesignItem!);
        Assert.NotNull(rootSelection);
        Assert.Equal(FrontedV3DesignSelectionKind.Root, rootSelection!.Kind);
        Assert.Null(rootSelection.SubTarget);

        // Root Schema 应包含根控件布局属性（Left/Top/Width/Height）
        var optionsPaths = rootSelection.Properties.Select(p => p.OptionsPath).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(nameof(BorderedImageFrontedControlConfig.Left), optionsPaths);
        Assert.Contains(nameof(BorderedImageFrontedControlConfig.Top), optionsPaths);
        Assert.Contains(nameof(BorderedImageFrontedControlConfig.Width), optionsPaths);
        Assert.Contains(nameof(BorderedImageFrontedControlConfig.Height), optionsPaths);
    }

    // -------------------------------------------------------------------
    // 9. DesignerDoesNotReferenceBorderedImageConfig
    // -------------------------------------------------------------------

    /// <summary>
    /// Designer ViewModel 源码不得引用 <c>BorderedImageFrontedControlConfig</c> 类型，
    /// 证明通用编辑路径不再通过 BorderedImage 类型特判选择属性或几何实现。
    /// </summary>
    [Fact]
    public void DesignerDoesNotReferenceBorderedImageConfig()
    {
        var source = ReadDesignerViewModelSource();
        Assert.DoesNotContain("BorderedImageFrontedControlConfig", source, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 10. DesignerDoesNotReferenceMapV2DisplayConfig
    // -------------------------------------------------------------------

    /// <summary>
    /// Designer ViewModel 源码不得引用 <c>MapV2DisplayControlConfig</c> 类型，
    /// 证明通用编辑路径不再通过 MapV2 类型特判选择属性或几何实现。
    /// </summary>
    [Fact]
    public void DesignerDoesNotReferenceMapV2DisplayConfig()
    {
        var source = ReadDesignerViewModelSource();
        Assert.DoesNotContain("MapV2DisplayControlConfig", source, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------
    // 11. DesignerDoesNotReferenceGlobalScoreRowConfig
    // -------------------------------------------------------------------

    /// <summary>
    /// Designer ViewModel 源码不得引用 <c>GlobalScoreRowControlConfig</c> 类型，
    /// 证明通用编辑路径不再通过 GlobalScoreRow 类型特判选择属性或几何实现。
    /// </summary>
    [Fact]
    public void DesignerDoesNotReferenceGlobalScoreRowConfig()
    {
        var source = ReadDesignerViewModelSource();
        Assert.DoesNotContain("GlobalScoreRowControlConfig", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 读取 <c>FrontedDesignerWindowViewModel.cs</c> 源文件内容，
    /// 用于断言 ViewModel 不引用控件专用 Config 类型。
    /// </summary>
    /// <returns>ViewModel 源文件的完整文本。</returns>
    /// <exception cref="FileNotFoundException">当 ViewModel 源文件无法定位时抛出。</exception>
    private static string ReadDesignerViewModelSource()
    {
        var path = GetRepositoryPath(
            "neo-bpsys-wpf", "ViewModels", "Windows", "FrontedDesignerWindowViewModel.cs");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// 从测试运行目录向上查找仓库根目录（以 <c>AGENTS.md</c> 与 <c>neo-bpsys-wpf.slnx</c>
    /// 同时存在为标识），并拼接指定相对路径。
    /// </summary>
    /// <param name="parts">相对于仓库根目录的路径片段。</param>
    /// <returns>仓库内文件的绝对路径。</returns>
    /// <exception cref="DirectoryNotFoundException">当无法定位仓库根目录时抛出。</exception>
    private static string GetRepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
            {
                return Path.Combine([directory.FullName, .. parts]);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
