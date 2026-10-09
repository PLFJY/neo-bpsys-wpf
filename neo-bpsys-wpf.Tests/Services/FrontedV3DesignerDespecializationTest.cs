using System;
using System.IO;
using System.Linq;
using neo_bpsys_wpf.Controls.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Properties;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>覆盖选择属性、插件 schema、属性写入和几何编辑的可观察结果。</summary>
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

        var builder = new FrontedV3DesignSelectionBuilder(CreateTestRegistry());
        var selection = builder.BuildRootSelection(designItem);

        Assert.NotNull(selection);
        Assert.Equal(FrontedV3DesignSelectionKind.Root, selection!.Kind);
        Assert.Null(selection.SubTarget);
        Assert.True(selection.HasEditableSchema);

        // Schema 中应包含外观属性，证明由反射生成而非控件类型特判
        var optionsPaths = selection.Properties.Select(p => p.OptionsPath).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(nameof(TextFrontedControlConfig.Color), optionsPaths);
        Assert.Contains(nameof(TextFrontedControlConfig.FontSize), optionsPaths);

        // 根几何字段由 Host 统一应用，但必须由通用根选择 Schema 暴露给属性面板，
        // 因此内置和插件 v3 控件都能编辑 X/Y/宽高/层级。
        Assert.Contains(nameof(TextFrontedControlConfig.Left), optionsPaths);
        Assert.Contains(nameof(TextFrontedControlConfig.Top), optionsPaths);
        Assert.Contains(nameof(TextFrontedControlConfig.Width), optionsPaths);
        Assert.Contains(nameof(TextFrontedControlConfig.Height), optionsPaths);
        Assert.Contains(nameof(TextFrontedControlConfig.ZIndex), optionsPaths);
        Assert.DoesNotContain(nameof(FrontedControlConfigBase.BehaviorGuid), optionsPaths);
        Assert.DoesNotContain(nameof(FrontedControlConfigBase.ControlType), optionsPaths);

        selection.Properties.Single(property => property.OptionsPath == nameof(TextFrontedControlConfig.Left))
            .SetValue(config, 40D);
        selection.Properties.Single(property => property.OptionsPath == nameof(TextFrontedControlConfig.Top))
            .SetValue(config, 50D);
        selection.Properties.Single(property => property.OptionsPath == nameof(TextFrontedControlConfig.Width))
            .SetValue(config, 120D);
        selection.Properties.Single(property => property.OptionsPath == nameof(TextFrontedControlConfig.Height))
            .SetValue(config, 60D);
        selection.Properties.Single(property => property.OptionsPath == nameof(TextFrontedControlConfig.ZIndex))
            .SetValue(config, 9);

        Assert.Equal(40D, config.Left);
        Assert.Equal(50D, config.Top);
        Assert.Equal(120D, config.Width);
        Assert.Equal(60D, config.Height);
        Assert.Equal(9, config.ZIndex);
    }

    /// <summary>
    /// 未在 Registry 中注册的控件调用 <see cref="FrontedV3DesignSelectionBuilder.BuildRootSelection"/>
    /// 时返回 <see langword="null"/>，表示 Missing Plugin。
    /// </summary>
    /// <remarks>
    /// Designer 去特化后，属性 Schema 仅从 Registration 读取，不再回退到 CLR 反射。
    /// 未注册的 ControlType（如缺失插件）返回 <see langword="null"/>，由 PropertyGridBuilder
    /// 通过 <c>AddMissingPluginRows</c> 显示诊断行。
    /// </remarks>
    [Fact]
    public void RootSelectionReturnsNullWhenSchemaIsEmpty()
    {
        var designItem = new FrontedControlDesignItem
        {
            Name = "Empty",
            Config = new FrontedControlConfigBase { ControlType = "Unknown" }
        };

        var builder = new FrontedV3DesignSelectionBuilder(CreateTestRegistry());
        var selection = builder.BuildRootSelection(designItem);

        // "Unknown" 未注册，Registry 返回 null Registration，selection 为 null。
        Assert.Null(selection);
    }

    /// <summary>
    /// 已注册控件即使没有控件专属 Schema 属性，调用 <see cref="FrontedV3DesignSelectionBuilder.BuildRootSelection"/>
    /// 也必须返回非空 Selection，并提供所有 v3 控件共享的根布局属性。
    /// </summary>
    [Fact]
    public void RootSelection_EmptyPropertiesForRegisteredControlWithoutSchema()
    {
        // 使用一个没有 Schema 属性的注册控件
        var registry = new FrontedV3ControlRegistry(
        [
            new FrontedV3ControlRegistration
            {
                ControlType = typeof(EmptySchemaControl),
                ConfigType = typeof(FrontedControlConfigBase),
                CanonicalControlType = "EmptySchema",
                LocalControlId = "EmptySchema",
                PackageId = "test",
                IsBuiltIn = false,
                Properties = Array.Empty<FrontedV3PropertyDefinition>(),
                FixedParts = Array.Empty<neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts.FrontedV3PartDefinition>(),
                PartCollections = Array.Empty<neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts.FrontedV3PartCollectionDefinition>(),
                CreateDefaultConfig = () => new FrontedControlConfigBase { ControlType = "EmptySchema" }
            }
        ]);

        var designItem = new FrontedControlDesignItem
        {
            Name = "Empty",
            Config = new FrontedControlConfigBase { ControlType = "EmptySchema" }
        };

        var builder = new FrontedV3DesignSelectionBuilder(registry);
        var selection = builder.BuildRootSelection(designItem);

        Assert.NotNull(selection);
        var optionsPaths = selection!.Properties.Select(property => property.OptionsPath).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(5, optionsPaths.Count);
        Assert.Contains(nameof(FrontedControlConfigBase.Left), optionsPaths);
        Assert.Contains(nameof(FrontedControlConfigBase.Top), optionsPaths);
        Assert.Contains(nameof(FrontedControlConfigBase.Width), optionsPaths);
        Assert.Contains(nameof(FrontedControlConfigBase.Height), optionsPaths);
        Assert.Contains(nameof(FrontedControlConfigBase.ZIndex), optionsPaths);
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

        var builder = new FrontedV3DesignSelectionBuilder(CreateTestRegistry());
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

        var builder = new FrontedV3DesignSelectionBuilder(CreateTestRegistry());
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

        var builder = new FrontedV3DesignSelectionBuilder(CreateTestRegistry());

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
        foreach (var name in new[] { "Color", "FontFamily", "FontWeight", "FontSize", "ShowCampIcon", "CampIconColor", "Visibility" })
            Assert.Contains(name, optionsPaths);
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

        var builder = new FrontedV3DesignSelectionBuilder(CreateTestRegistry());
        var selection = builder.BuildCollectionItemSelection(
            designItem, collectionId: "NonExistent", itemKey: "SomeKey");

        Assert.Null(selection);
    }

    // -------------------------------------------------------------------
    // 3a. SelectCollectionItem_IncludesAppearanceProperties
    // -------------------------------------------------------------------

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

        var builder = new FrontedV3DesignSelectionBuilder(CreateTestRegistry());

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

        var builder = new FrontedV3DesignSelectionBuilder(CreateTestRegistry());

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
    /// V3 属性 Schema 应将图片覆盖层的独立开关和拉伸枚举放入 Overlay 分组，
    /// 并为它们声明对应的编辑器类型。
    /// </summary>
    [Fact]
    public void ImageOverlayStretchPropertiesUseSchemaEditors()
    {
        var properties = BuiltInPropertyDefinitionResolver.GetProperties(
            new BorderedImageFrontedControlConfig());

        var lockToggle = properties.Single(property =>
            property.OptionsPath == nameof(ImageFrontedControlConfig.UseIndependentLockStretch));
        var pickingToggle = properties.Single(property =>
            property.OptionsPath == nameof(ImageFrontedControlConfig.UseIndependentPickingBorderStretch));
        var lockAvailable = properties.Single(property =>
            property.OptionsPath == nameof(ImageFrontedControlConfig.Lockable));
        var pickingAvailable = properties.Single(property =>
            property.OptionsPath == nameof(ImageFrontedControlConfig.PickingBorderAvailable));
        var lockStretch = properties.Single(property =>
            property.OptionsPath == nameof(ImageFrontedControlConfig.LockStretch));
        var pickingStretch = properties.Single(property =>
            property.OptionsPath == nameof(ImageFrontedControlConfig.PickingBorderStretch));

        Assert.Equal("Overlay", lockToggle.Metadata.GroupName);
        Assert.Equal("Overlay", pickingToggle.Metadata.GroupName);
        Assert.Equal("Overlay", lockStretch.Metadata.GroupName);
        Assert.Equal("Overlay", pickingStretch.Metadata.GroupName);
        Assert.Equal(FrontedPropertyEditorKind.ToggleSwitch, lockToggle.Metadata.EditorKind);
        Assert.Equal(FrontedPropertyEditorKind.ToggleSwitch, pickingToggle.Metadata.EditorKind);
        Assert.Equal(FrontedPropertyEditorKind.ToggleSwitch, lockAvailable.Metadata.EditorKind);
        Assert.Equal(FrontedPropertyEditorKind.ToggleSwitch, pickingAvailable.Metadata.EditorKind);
        Assert.Equal(FrontedPropertyEditorKind.Enum, lockStretch.Metadata.EditorKind);
        Assert.Equal(FrontedPropertyEditorKind.Enum, pickingStretch.Metadata.EditorKind);
    }

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
        colorProperty!.SetValue(config, newValue);
        var fontSizeProperty = properties.First(p => p.OptionsPath == nameof(TextFrontedControlConfig.FontSize));
        fontSizeProperty.SetValue(config, 32D);
        Assert.Equal(32D, fontSizeProperty.GetValue(config));

        // Config 的 Color 字段被更新
        Assert.Equal(newValue, config.Color);

        Assert.Equal(32, config.FontSize);
        // 未编辑的文本保持不变
        Assert.Equal("Hello", config.Text);

        // 反向读取：Storage.GetValue 返回当前 Config 值
        Assert.Equal(newValue, colorProperty.Storage.GetValue(config));
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
    // 7. GeometryRoundtripWorksForAllGeometryTargets
    // -------------------------------------------------------------------

    /// <summary>
    /// 几何数据往返对所有 <see cref="IFrontedV3GeometryTarget"/> 类型工作：
    /// 记录原始几何值 → 通过 GeometryTarget 修改 → 验证已修改 →
    /// 通过 GeometryTarget 恢复原始值 → 验证已恢复。
    /// 此测试不执行撤销栈；覆盖 Root/FixedPart/CollectionItem 三种 GeometryTarget。
    /// </summary>
    [Fact]
    public void GeometryRoundtripWorksForAllGeometryTargets()
    {
        GeometryRoundtripWorksForRootGeometryTarget();
        GeometryRoundtripWorksForFixedPartGeometryTarget();
        GeometryRoundtripWorksForCollectionItemGeometryTarget();
    }

    /// <summary>
    /// Root GeometryTarget 的几何往返：记录 Config 原始几何 → ResizeTo 修改 →
    /// ResizeTo 恢复 → 验证 Config 几何已恢复。
    /// </summary>
    private static void GeometryRoundtripWorksForRootGeometryTarget()
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

        // 恢复原始几何
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
    /// FixedPart GeometryTarget 的几何往返：记录 MapV2 Part 原始几何 →
    /// ResizeTo 修改 → ResizeTo 恢复 → 验证 InternalParts 项几何已恢复。
    /// </summary>
    private static void GeometryRoundtripWorksForFixedPartGeometryTarget()
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

        // 恢复原始几何
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
    /// CollectionItem GeometryTarget 的几何往返：记录 GlobalScoreRow Cell 原始几何 →
    /// ResizeTo 修改 → ResizeTo 恢复 → 验证 Cell 几何已恢复。
    /// </summary>
    private static void GeometryRoundtripWorksForCollectionItemGeometryTarget()
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

        // 恢复原始几何
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

    /// <summary>
    /// 创建包含本测试所需内置控件（Text、BorderedImage、GlobalScoreRow、MapV2Display）
    /// 的 <see cref="IFrontedV3ControlRegistry"/>，属性 Schema、FixedParts、PartCollections
    /// 通过 <see cref="BuiltInPropertyDefinitionResolver"/>、<see cref="BuiltInPartDefinitionResolver"/>、
    /// <see cref="BuiltInPartCollectionDefinitionResolver"/> 生成，与生产注册路径保持一致。
    /// </summary>
    /// <returns>用于测试的 <see cref="IFrontedV3ControlRegistry"/> 实例。</returns>
    private static IFrontedV3ControlRegistry CreateTestRegistry()
    {
        return new FrontedV3ControlRegistry(
        [
            CreateBuiltInRegistration("Text", typeof(TextFrontedControl), typeof(TextFrontedControlConfig), () => new TextFrontedControlConfig { Text = "Text" }),
            CreateBuiltInRegistration("BorderedImage", typeof(BorderedImageFrontedControl), typeof(BorderedImageFrontedControlConfig), () => new BorderedImageFrontedControlConfig()),
            CreateBuiltInRegistration("GlobalScoreRow", typeof(GlobalScoreRowFrontedControl), typeof(GlobalScoreRowControlConfig), () => new GlobalScoreRowControlConfig()),
            CreateBuiltInRegistration("MapV2Display", typeof(MapV2DisplayControlConfig), typeof(MapV2DisplayControlConfig), () => new MapV2DisplayControlConfig())
        ]);
    }

    /// <summary>
    /// 创建一个内置控件的 <see cref="FrontedV3ControlRegistration"/>，
    /// 属性 Schema、FixedParts、PartCollections 由内置 Resolver 反射生成。
    /// </summary>
    /// <param name="controlId">控件局部标识，同时作为 CanonicalControlType。</param>
    /// <param name="controlType">控件 <see cref="Type"/>。</param>
    /// <param name="configType">配置 <see cref="Type"/>。</param>
    /// <param name="createDefaultConfig">创建默认配置的工厂。</param>
    /// <returns>填充完整的 <see cref="FrontedV3ControlRegistration"/>。</returns>
    private static FrontedV3ControlRegistration CreateBuiltInRegistration(
        string controlId,
        Type controlType,
        Type configType,
        Func<FrontedControlConfigBase> createDefaultConfig)
    {
        var sampleConfig = (FrontedControlConfigBase)Activator.CreateInstance(configType)!;
        sampleConfig.ControlType = controlId;

        return new FrontedV3ControlRegistration
        {
            CanonicalControlType = controlId,
            LocalControlId = controlId,
            PackageId = null,
            IsBuiltIn = true,
            ControlType = controlType,
            ConfigType = configType,
            Properties = BuiltInPropertyDefinitionResolver.GetProperties(sampleConfig),
            FixedParts = BuiltInPartDefinitionResolver.GetParts(sampleConfig),
            PartCollections = BuiltInPartCollectionDefinitionResolver.GetCollections(sampleConfig),
            CreateDefaultConfig = createDefaultConfig
        };
    }
}

/// <summary>
/// 用于 <see cref="FrontedV3DesignerDespecializationTest.RootSelection_EmptyPropertiesForRegisteredControlWithoutSchema"/>
/// 的占位控件类型，仅用于构造没有 Schema 属性的 Registration。
/// </summary>
internal sealed class EmptySchemaControl;
