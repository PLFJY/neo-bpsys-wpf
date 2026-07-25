using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.PluginSdk;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.ViewModels.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Phase 3 SubTask 4.6 测试：覆盖固定 Part 体系、Visual 发现、Geometry/Capabilities、
/// BorderedImage 迁移与 Designer 去特化的全部场景。
/// </summary>
/// <remarks>
/// <para>
/// 这些测试验证 Phase 3 的核心契约：
/// <list type="bullet">
/// <item>Part Visual 发现同时支持 XAML 附加属性与 C# 特性。</item>
/// <item>缺失/重复 Visual 输出诊断，不崩溃。</item>
/// <item>FixedPartGeometryTarget 通过 Storage 读写 Config，遵守 Capabilities 约束。</item>
/// <item>BorderedImage 内层 Image 注册为 Resize-only Part，JSON 根级字段不变。</item>
/// <item>Designer 不再包含 BorderedImage 专用特判代码。</item>
/// </list>
/// </para>
/// <para>
/// 涉及 WPF 视觉树的操作在 STA 线程上执行，遵循 <see cref="WpfTestThread"/> 规范。
/// </para>
/// </remarks>
public class FrontedV3PartTest
{
    // -------------------------------------------------------------------
    // 1. PartVisualIsDiscovered
    // -------------------------------------------------------------------

    /// <summary>
    /// XAML 附加属性 <c>fronted:FrontedV3.PartId</c> 与 C# 特性
    /// <c>[FrontedV3PartVisual]</c> 标注的 Visual 都必须被发现并映射到对应 Part。
    /// </summary>
    [Fact]
    public void PartVisualIsDiscovered()
    {
        WpfTestThread.Run(() =>
        {
            // XAML 附加属性方式
            var border = new Border();
            var innerImage = new Image();
            FrontedV3.SetPartId(innerImage, "Logo");
            border.Child = innerImage;

            var partDefinitions = new[]
            {
                new FrontedV3PartDefinition(id: "Logo", capabilities: FrontedV3PartCapabilities.Resize)
            };

            var result = FrontedV3PartVisualResolver.Resolve(border, partDefinitions);

            Assert.True(result.DiscoveredVisuals.ContainsKey("Logo"));
            Assert.Same(innerImage, result.DiscoveredVisuals["Logo"]);
            Assert.False(result.HasDiagnostics);
        });
    }

    /// <summary>
    /// C# 特性 <c>[FrontedV3PartVisual]</c> 标注的属性返回的 Visual 必须被发现。
    /// </summary>
    [Fact]
    public void PartVisualIsDiscoveredFromAttribute()
    {
        WpfTestThread.Run(() =>
        {
            var control = new AttributeAnnotatedControl();
            var partDefinitions = new[]
            {
                new FrontedV3PartDefinition(id: "Logo", capabilities: FrontedV3PartCapabilities.Resize)
            };

            var result = FrontedV3PartVisualResolver.Resolve(control, partDefinitions);

            Assert.True(result.DiscoveredVisuals.ContainsKey("Logo"));
            Assert.Same(control.LogoElement, result.DiscoveredVisuals["Logo"]);
            Assert.False(result.HasDiagnostics);
        });
    }

    // -------------------------------------------------------------------
    // 2. MissingPartVisualProducesDiagnostic
    // -------------------------------------------------------------------

    /// <summary>
    /// 声明了 Part 但未找到对应 Visual 时，必须输出诊断 warning，不崩溃。
    /// </summary>
    [Fact]
    public void MissingPartVisualProducesDiagnostic()
    {
        WpfTestThread.Run(() =>
        {
            var border = new Border();
            var partDefinitions = new[]
            {
                new FrontedV3PartDefinition(id: "Logo", capabilities: FrontedV3PartCapabilities.Resize)
            };

            var result = FrontedV3PartVisualResolver.Resolve(border, partDefinitions);

            Assert.False(result.DiscoveredVisuals.ContainsKey("Logo"));
            Assert.True(result.HasDiagnostics);
            Assert.Contains(result.Diagnostics, d =>
                d.PartId == "Logo" &&
                d.Severity == FrontedV3PartVisualDiagnosticSeverity.Warning);
        });
    }

    // -------------------------------------------------------------------
    // 3. DuplicatePartVisualProducesDiagnostic
    // -------------------------------------------------------------------

    /// <summary>
    /// 多个 Visual 映射到同一 PartId 时，必须输出诊断 warning，使用第一个发现的 Visual。
    /// </summary>
    [Fact]
    public void DuplicatePartVisualProducesDiagnostic()
    {
        WpfTestThread.Run(() =>
        {
            var border = new Border();
            var stackPanel = new StackPanel();
            var firstImage = new Image();
            var secondImage = new Image();
            FrontedV3.SetPartId(firstImage, "Logo");
            FrontedV3.SetPartId(secondImage, "Logo");
            stackPanel.Children.Add(firstImage);
            stackPanel.Children.Add(secondImage);
            border.Child = stackPanel;

            var partDefinitions = new[]
            {
                new FrontedV3PartDefinition(id: "Logo", capabilities: FrontedV3PartCapabilities.Resize)
            };

            var result = FrontedV3PartVisualResolver.Resolve(border, partDefinitions);

            Assert.True(result.DiscoveredVisuals.ContainsKey("Logo"));
            Assert.Same(firstImage, result.DiscoveredVisuals["Logo"]);
            Assert.True(result.HasDiagnostics);
            Assert.Contains(result.Diagnostics, d =>
                d.PartId == "Logo" &&
                d.Severity == FrontedV3PartVisualDiagnosticSeverity.Warning &&
                d.Message.Contains("Duplicate", StringComparison.Ordinal));
        });
    }

    // -------------------------------------------------------------------
    // 4. ResizeWritesExistingImageWidthHeightFields
    // -------------------------------------------------------------------

    /// <summary>
    /// 通过 <see cref="FixedPartGeometryTarget"/> 缩放 BorderedImage 的 Image Part 时，
    /// 必须写入 Config 的 <c>ImageWidth</c>/<c>ImageHeight</c> 根级字段。
    /// </summary>
    [Fact]
    public void ResizeWritesExistingImageWidthHeightFields()
    {
        var config = new BorderedImageFrontedControlConfig
        {
            ImageWidth = 60,
            ImageHeight = 40
        };

        var parts = BuiltInPartDefinitionResolver.GetParts(config);
        Assert.Single(parts);
        Assert.Equal("Image", parts[0].Id);

        var target = new FixedPartGeometryTarget(parts[0], config);
        target.ResizeTo(left: 0, top: 0, width: 120, height: 80);

        Assert.Equal(120, config.ImageWidth);
        Assert.Equal(80, config.ImageHeight);
    }

    // -------------------------------------------------------------------
    // 5. ResizeOnlyPartCannotMove
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3PartCapabilities.Resize"/> 能力的 Part 调用
    /// <see cref="FixedPartGeometryTarget.MoveTo"/> 时不得写入 X/Y 存储。
    /// </summary>
    [Fact]
    public void ResizeOnlyPartCannotMove()
    {
        var config = new PluginFrontedControlConfig { ControlType = "plugin:test/ResizeOnly" };
        config.ExtensionData["PartX"] = JsonSerializer.SerializeToElement(10D);
        config.ExtensionData["PartY"] = JsonSerializer.SerializeToElement(20D);

        var part = new FrontedV3PartDefinition(
            id: "ResizeOnlyPart",
            capabilities: FrontedV3PartCapabilities.Resize,
            widthStorage: FrontedV3Storage.ExtensionData("PartWidth"),
            heightStorage: FrontedV3Storage.ExtensionData("PartHeight"),
            xStorage: FrontedV3Storage.ExtensionData("PartX"),
            yStorage: FrontedV3Storage.ExtensionData("PartY"));

        var target = new FixedPartGeometryTarget(part, config);

        // MoveTo 应被 Capabilities 阻止
        target.MoveTo(left: 100, top: 200);

        Assert.Equal(10D, config.ExtensionData["PartX"].GetDouble());
        Assert.Equal(20D, config.ExtensionData["PartY"].GetDouble());
    }

    /// <summary>
    /// <see cref="FrontedV3PartCapabilities.Move"/> 能力的 Part 调用
    /// <see cref="FixedPartGeometryTarget.ResizeTo"/> 时不得写入 Width/Height 存储。
    /// </summary>
    [Fact]
    public void MoveOnlyPartCannotResize()
    {
        var config = new PluginFrontedControlConfig { ControlType = "plugin:test/MoveOnly" };
        config.ExtensionData["PartWidth"] = JsonSerializer.SerializeToElement(50D);
        config.ExtensionData["PartHeight"] = JsonSerializer.SerializeToElement(30D);

        var part = new FrontedV3PartDefinition(
            id: "MoveOnlyPart",
            capabilities: FrontedV3PartCapabilities.Move,
            widthStorage: FrontedV3Storage.ExtensionData("PartWidth"),
            heightStorage: FrontedV3Storage.ExtensionData("PartHeight"),
            xStorage: FrontedV3Storage.ExtensionData("PartX"),
            yStorage: FrontedV3Storage.ExtensionData("PartY"));

        var target = new FixedPartGeometryTarget(part, config);

        // ResizeTo 应被 Capabilities 阻止
        target.ResizeTo(left: 0, top: 0, width: 999, height: 888);

        Assert.Equal(50D, config.ExtensionData["PartWidth"].GetDouble());
        Assert.Equal(30D, config.ExtensionData["PartHeight"].GetDouble());
    }

    // -------------------------------------------------------------------
    // 6. PartBoundsClampToParent
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FixedPartGeometryTarget.ClampToParent"/> 必须将 Part 几何限制在父 Control 边界内。
    /// </summary>
    [Fact]
    public void PartBoundsClampToParent()
    {
        // X 为负时被限制为 0
        var clamped1 = FixedPartGeometryTarget.ClampToParent(
            new FrontedV3PartGeometry(x: -10, y: 0, width: 50, height: 50),
            parentWidth: 200,
            parentHeight: 200);
        Assert.Equal(0, clamped1.X);

        // X + Width 超出父宽度时被限制
        var clamped2 = FixedPartGeometryTarget.ClampToParent(
            new FrontedV3PartGeometry(x: 180, y: 0, width: 50, height: 50),
            parentWidth: 200,
            parentHeight: 200);
        Assert.Equal(150, clamped2.X);

        // Width 超过父宽度时被限制为父宽度
        var clamped3 = FixedPartGeometryTarget.ClampToParent(
            new FrontedV3PartGeometry(x: 0, y: 0, width: 300, height: 50),
            parentWidth: 200,
            parentHeight: 200);
        Assert.Equal(200, clamped3.Width);

        // Y 为负时被限制为 0
        var clamped4 = FixedPartGeometryTarget.ClampToParent(
            new FrontedV3PartGeometry(x: 0, y: -20, width: 50, height: 50),
            parentWidth: 200,
            parentHeight: 200);
        Assert.Equal(0, clamped4.Y);

        // Y + Height 超出父高度时被限制
        var clamped5 = FixedPartGeometryTarget.ClampToParent(
            new FrontedV3PartGeometry(x: 0, y: 190, width: 50, height: 50),
            parentWidth: 200,
            parentHeight: 200);
        Assert.Equal(150, clamped5.Y);

        // Height 超过父高度时被限制为父高度
        var clamped6 = FixedPartGeometryTarget.ClampToParent(
            new FrontedV3PartGeometry(x: 0, y: 0, width: 50, height: 300),
            parentWidth: 200,
            parentHeight: 200);
        Assert.Equal(200, clamped6.Height);

        // parentWidth/parentHeight 非正时不限制对应维度
        var clamped7 = FixedPartGeometryTarget.ClampToParent(
            new FrontedV3PartGeometry(x: -100, y: -100, width: 999, height: 999),
            parentWidth: 0,
            parentHeight: 0);
        Assert.Equal(-100, clamped7.X);
        Assert.Equal(-100, clamped7.Y);
        Assert.Equal(999, clamped7.Width);
        Assert.Equal(999, clamped7.Height);
    }

    // -------------------------------------------------------------------
    // 7. BorderedImageJsonIsUnchanged
    // -------------------------------------------------------------------

    /// <summary>
    /// BorderedImageFrontedControlConfig 序列化后 ImageWidth/ImageHeight 必须在 JSON 根级，
    /// 不出现 Options 嵌套对象；反序列化往返后值保持一致。
    /// </summary>
    [Fact]
    public void BorderedImageJsonIsUnchanged()
    {
        var config = new BorderedImageFrontedControlConfig
        {
            Left = 10,
            Top = 20,
            Width = 120,
            Height = 80,
            ImageWidth = 60,
            ImageHeight = 40
        };

        var json = JsonSerializer.Serialize(config);

        // 根级字段存在
        Assert.Contains("\"ImageWidth\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ImageHeight\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ControlType\"", json, StringComparison.Ordinal);
        Assert.Contains("\"BorderedImage\"", json, StringComparison.Ordinal);

        // 不出现 Options 嵌套对象
        Assert.DoesNotContain("\"Options\"", json, StringComparison.Ordinal);

        // 反序列化往返
        var deserialized = JsonSerializer.Deserialize<BorderedImageFrontedControlConfig>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(60, deserialized!.ImageWidth);
        Assert.Equal(40, deserialized.ImageHeight);
        Assert.Equal(120, deserialized.Width);
        Assert.Equal(80, deserialized.Height);
        Assert.Equal("BorderedImage", deserialized.ControlType);
    }

    /// <summary>
    /// BorderedImageFrontedControlConfig 的 ImageWidth/ImageHeight 为 null 时，
    /// JSON 仍包含这两个字段（值为 null）；反序列化后仍为 null。
    /// </summary>
    [Fact]
    public void BorderedImageJsonPreservesNullImageDimensions()
    {
        var config = new BorderedImageFrontedControlConfig
        {
            Left = 10,
            Top = 20,
            Width = 120,
            Height = 80
        };

        var json = JsonSerializer.Serialize(config);

        // ImageWidth/ImageHeight 为 null 时仍以 null 值出现在 JSON 根级
        Assert.Contains("\"ImageWidth\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"ImageHeight\":null", json, StringComparison.Ordinal);

        // 反序列化往返
        var deserialized = JsonSerializer.Deserialize<BorderedImageFrontedControlConfig>(json);
        Assert.NotNull(deserialized);
        Assert.Null(deserialized!.ImageWidth);
        Assert.Null(deserialized.ImageHeight);
    }

    // -------------------------------------------------------------------
    // 8. DesignerNoLongerUsesBorderedImageSpecialCase
    // -------------------------------------------------------------------

    /// <summary>
    /// Designer ViewModel 不得包含 BorderedImage 专用特判成员：
    /// <c>BorderedImageResizeTarget</c>、<c>IsBorderResizeTargetSelected</c>、
    /// <c>IsImageResizeTargetSelected</c>、<c>IsBorderedImageSelected</c>、
    /// <c>ResizeSelectedBorderedImageInnerImage</c>。
    /// <c>FrontedDesignerResizeTarget</c> 枚举类型不得存在。
    /// </summary>
    [Fact]
    public void DesignerNoLongerUsesBorderedImageSpecialCase()
    {
        var viewModelType = typeof(FrontedDesignerWindowViewModel);

        // ViewModel 不得包含这些属性
        Assert.Null(viewModelType.GetProperty(
            "BorderedImageResizeTarget",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Null(viewModelType.GetProperty(
            "IsBorderResizeTargetSelected",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Null(viewModelType.GetProperty(
            "IsImageResizeTargetSelected",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Null(viewModelType.GetProperty(
            "IsBorderedImageSelected",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

        // ViewModel 不得包含这个方法
        var method = viewModelType.GetMethod(
            "ResizeSelectedBorderedImageInnerImage",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.Null(method);

        // FrontedDesignerResizeTarget 枚举类型不得存在
        var resizeTargetType = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => SafeGetTypes(a))
            .FirstOrDefault(t => t.Name == "FrontedDesignerResizeTarget" && t.IsEnum);
        Assert.Null(resizeTargetType);
    }

    /// <summary>
    /// BorderedImage 的 Part 定义必须由 <see cref="BuiltInPartDefinitionResolver"/> 正确提供：
    /// Id=Image、Capabilities=Resize、WidthStorage=ImageWidth、HeightStorage=ImageHeight。
    /// </summary>
    [Fact]
    public void BuiltInPartDefinitionResolverProvidesBorderedImagePart()
    {
        var config = new BorderedImageFrontedControlConfig();

        Assert.True(BuiltInPartDefinitionResolver.HasParts(config));

        var parts = BuiltInPartDefinitionResolver.GetParts(config);
        Assert.Single(parts);

        var part = parts[0];
        Assert.Equal("Image", part.Id);
        Assert.True(part.Capabilities.CanResize);
        Assert.False(part.Capabilities.CanMove);
        Assert.Equal("ImageWidth", part.WidthStorage?.TargetField);
        Assert.Equal("ImageHeight", part.HeightStorage?.TargetField);
        Assert.Null(part.XStorage);
        Assert.Null(part.YStorage);
    }

    /// <summary>
    /// 非 BorderedImage 的 Config 不得返回 Part 定义。
    /// </summary>
    [Fact]
    public void BuiltInPartDefinitionResolverReturnsEmptyForNonBorderedImage()
    {
        var config = new TextFrontedControlConfig();
        Assert.False(BuiltInPartDefinitionResolver.HasParts(config));
        Assert.Empty(BuiltInPartDefinitionResolver.GetParts(config));
    }

    /// <summary>
    /// <see cref="BuiltInPartDefinitionResolver.FindPart"/> 按 Id 查找 Part 定义。
    /// </summary>
    [Fact]
    public void BuiltInPartDefinitionResolverFindPartById()
    {
        var config = new BorderedImageFrontedControlConfig();

        var found = BuiltInPartDefinitionResolver.FindPart(config, "Image");
        Assert.NotNull(found);
        Assert.Equal("Image", found!.Id);

        var notFound = BuiltInPartDefinitionResolver.FindPart(config, "NonExistent");
        Assert.Null(notFound);
    }

    /// <summary>
    /// <see cref="FrontedV3Part.Register{TControl}"/> 链式 API 必须正确生成
    /// <see cref="FrontedV3PartDefinition"/>。
    /// </summary>
    [Fact]
    public void FrontedV3PartRegisterProducesCorrectDefinition()
    {
        var part = FrontedV3Part.Register<DummyControl>("Logo")
            .WithSize(
                FrontedV3Storage.ClrProperty("LogoWidth"),
                FrontedV3Storage.ClrProperty("LogoHeight"))
            .WithPosition(
                FrontedV3Storage.ClrProperty("LogoX"),
                FrontedV3Storage.ClrProperty("LogoY"))
            .WithCapabilities(FrontedV3PartCapabilities.MoveAndResize);

        Assert.Equal("Logo", part.Id);
        Assert.Equal(typeof(DummyControl), part.ControlType);
        Assert.True(part.Capabilities.CanMove);
        Assert.True(part.Capabilities.CanResize);
        Assert.Equal("LogoWidth", part.WidthStorage?.TargetField);
        Assert.Equal("LogoHeight", part.HeightStorage?.TargetField);
        Assert.Equal("LogoX", part.XStorage?.TargetField);
        Assert.Equal("LogoY", part.YStorage?.TargetField);

        var definition = part.ToDefinition();
        Assert.Equal("Logo", definition.Id);
        Assert.True(definition.Capabilities.CanMove);
        Assert.True(definition.Capabilities.CanResize);
    }

    /// <summary>
    /// <see cref="FrontedV3Part.Discover"/> 从控件类型的 public static readonly 字段
    /// 发现所有 <see cref="FrontedV3Part"/> 声明。
    /// </summary>
    [Fact]
    public void FrontedV3PartDiscoverFindsDeclaredParts()
    {
        var definitions = FrontedV3Part.Discover(typeof(ControlWithPartDeclarations));

        Assert.Equal(2, definitions.Count);
        Assert.Contains(definitions, d => d.Id == "Logo");
        Assert.Contains(definitions, d => d.Id == "Badge");
    }

    /// <summary>
    /// <see cref="FrontedV3PartCapabilities"/> 预设实例必须正确设置 CanMove/CanResize。
    /// </summary>
    [Fact]
    public void PartCapabilitiesPresetsAreCorrect()
    {
        Assert.False(FrontedV3PartCapabilities.None.CanMove);
        Assert.False(FrontedV3PartCapabilities.None.CanResize);

        Assert.True(FrontedV3PartCapabilities.Move.CanMove);
        Assert.False(FrontedV3PartCapabilities.Move.CanResize);

        Assert.False(FrontedV3PartCapabilities.Resize.CanMove);
        Assert.True(FrontedV3PartCapabilities.Resize.CanResize);

        Assert.True(FrontedV3PartCapabilities.MoveAndResize.CanMove);
        Assert.True(FrontedV3PartCapabilities.MoveAndResize.CanResize);
    }

    /// <summary>
    /// <see cref="FrontedV3PartPropertyContext"/> 携带 Part 定义与 Config。
    /// </summary>
    [Fact]
    public void PartPropertyContextHoldsDefinitionAndConfig()
    {
        var config = new BorderedImageFrontedControlConfig();
        var part = BuiltInPartDefinitionResolver.GetParts(config)[0];

        var context = new FrontedV3PartPropertyContext(part, config);

        Assert.Same(part, context.PartDefinition);
        Assert.Same(config, context.Config);
    }

    /// <summary>
    /// <see cref="FrontedV3PartPropertyContext"/> 构造函数拒绝 null 参数。
    /// </summary>
    [Fact]
    public void PartPropertyContextRejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FrontedV3PartPropertyContext(null!, new BorderedImageFrontedControlConfig()));
        Assert.Throws<ArgumentNullException>(() =>
            new FrontedV3PartPropertyContext(
                new FrontedV3PartDefinition("X", FrontedV3PartCapabilities.None),
                null!));
    }

    /// <summary>
    /// <see cref="FixedPartGeometryTarget"/> 构造函数拒绝 null 参数。
    /// </summary>
    [Fact]
    public void FixedPartGeometryTargetRejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FixedPartGeometryTarget(null!, new BorderedImageFrontedControlConfig()));
        Assert.Throws<ArgumentNullException>(() =>
            new FixedPartGeometryTarget(
                new FrontedV3PartDefinition("X", FrontedV3PartCapabilities.None),
                null!));
    }

    /// <summary>
    /// MoveAndResize 能力的 Part 调用 MoveTo 时必须写入 X/Y 存储。
    /// </summary>
    [Fact]
    public void MoveAndResizePartMoveWritesXYStorage()
    {
        var config = new PluginFrontedControlConfig { ControlType = "plugin:test/MoveAndResize" };
        config.ExtensionData["PartX"] = JsonSerializer.SerializeToElement(10D);
        config.ExtensionData["PartY"] = JsonSerializer.SerializeToElement(20D);

        var part = new FrontedV3PartDefinition(
            id: "MoveAndResizePart",
            capabilities: FrontedV3PartCapabilities.MoveAndResize,
            xStorage: FrontedV3Storage.ExtensionData("PartX"),
            yStorage: FrontedV3Storage.ExtensionData("PartY"));

        var target = new FixedPartGeometryTarget(part, config);
        target.MoveTo(left: 100, top: 200);

        Assert.Equal(100D, config.ExtensionData["PartX"].GetDouble());
        Assert.Equal(200D, config.ExtensionData["PartY"].GetDouble());
    }

    /// <summary>
    /// MoveAndResize 能力的 Part 调用 ResizeTo 时必须同时写入 Width/Height 和 X/Y 存储。
    /// </summary>
    [Fact]
    public void MoveAndResizePartResizeWritesAllDimensions()
    {
        var config = new PluginFrontedControlConfig { ControlType = "plugin:test/MoveAndResize2" };
        config.ExtensionData["PartX"] = JsonSerializer.SerializeToElement(10D);
        config.ExtensionData["PartY"] = JsonSerializer.SerializeToElement(20D);
        config.ExtensionData["PartWidth"] = JsonSerializer.SerializeToElement(50D);
        config.ExtensionData["PartHeight"] = JsonSerializer.SerializeToElement(30D);

        var part = new FrontedV3PartDefinition(
            id: "MoveAndResizePart",
            capabilities: FrontedV3PartCapabilities.MoveAndResize,
            widthStorage: FrontedV3Storage.ExtensionData("PartWidth"),
            heightStorage: FrontedV3Storage.ExtensionData("PartHeight"),
            xStorage: FrontedV3Storage.ExtensionData("PartX"),
            yStorage: FrontedV3Storage.ExtensionData("PartY"));

        var target = new FixedPartGeometryTarget(part, config);
        target.ResizeTo(left: 100, top: 200, width: 120, height: 80);

        Assert.Equal(120D, config.ExtensionData["PartWidth"].GetDouble());
        Assert.Equal(80D, config.ExtensionData["PartHeight"].GetDouble());
        Assert.Equal(100D, config.ExtensionData["PartX"].GetDouble());
        Assert.Equal(200D, config.ExtensionData["PartY"].GetDouble());
    }

    /// <summary>
    /// <see cref="FixedPartGeometryTarget.Left"/>/<see cref="FixedPartGeometryTarget.Top"/>
    /// 在无 X/Y 存储时返回 0；<see cref="FixedPartGeometryTarget.Width"/>/<see cref="FixedPartGeometryTarget.Height"/>
    /// 在无 Width/Height 存储时返回 null。
    /// </summary>
    [Fact]
    public void FixedPartGeometryTargetReturnsDefaultsWhenStorageIsNull()
    {
        var config = new BorderedImageFrontedControlConfig { ImageWidth = 60, ImageHeight = 40 };
        var part = BuiltInPartDefinitionResolver.GetParts(config)[0];

        var target = new FixedPartGeometryTarget(part, config);

        // BorderedImage Part 没有 X/Y 存储
        Assert.Equal(0, target.Left);
        Assert.Equal(0, target.Top);
        // 有 Width/Height 存储
        Assert.Equal(60, target.Width);
        Assert.Equal(40, target.Height);
    }

    /// <summary>
    /// <see cref="FixedPartGeometryTarget.ApplyToVisual"/> 触发视觉同步回调。
    /// </summary>
    [Fact]
    public void FixedPartGeometryTargetApplyToVisualInvokesCallback()
    {
        var config = new BorderedImageFrontedControlConfig { ImageWidth = 60, ImageHeight = 40 };
        var part = BuiltInPartDefinitionResolver.GetParts(config)[0];

        var invoked = false;
        var target = new FixedPartGeometryTarget(part, config, () => invoked = true);

        target.ApplyToVisual();
        Assert.True(invoked);
    }

    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException)
        {
            return Array.Empty<Type>();
        }
    }
}

/// <summary>
/// 用于测试 C# 特性标注 Part Visual 的控件。
/// </summary>
public sealed class AttributeAnnotatedControl : FrameworkElement
{
    private readonly Border _logoBorder = new();

    /// <summary>
    /// 获取 Logo Visual，通过 <see cref="FrontedV3PartVisualAttribute"/> 标注为 PartId="Logo"。
    /// </summary>
    [FrontedV3PartVisual("Logo")]
    public FrameworkElement LogoElement => _logoBorder;
}

/// <summary>
/// 用于测试 <see cref="FrontedV3Part.Register{TControl}"/> 的占位控件类型。
/// </summary>
public sealed class DummyControl;

/// <summary>
/// 声明了多个 <see cref="FrontedV3Part"/> 字段的控件，用于测试 <see cref="FrontedV3Part.Discover"/>。
/// </summary>
public sealed class ControlWithPartDeclarations : FrameworkElement
{
    /// <summary>
    /// Logo Part 声明。
    /// </summary>
    public static readonly FrontedV3Part LogoPart =
        FrontedV3Part.Register<ControlWithPartDeclarations>("Logo")
            .WithSize(
                FrontedV3Storage.ClrProperty("LogoWidth"),
                FrontedV3Storage.ClrProperty("LogoHeight"))
            .WithCapabilities(FrontedV3PartCapabilities.Resize);

    /// <summary>
    /// Badge Part 声明。
    /// </summary>
    public static readonly FrontedV3Part BadgePart =
        FrontedV3Part.Register<ControlWithPartDeclarations>("Badge")
            .WithSize(
                FrontedV3Storage.ClrProperty("BadgeWidth"),
                FrontedV3Storage.ClrProperty("BadgeHeight"))
            .WithCapabilities(FrontedV3PartCapabilities.MoveAndResize);
}
