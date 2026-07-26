using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Options;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.PluginSdk;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// P0-任务 2 测试：验证 <see cref="FrontedV3PartVisualRuntimeBinder"/> 在 Host 创建控件后
/// 自动发现 Part Visual 并应用 Storage 几何，插件作者无需在 OnInitializeFrontedV3 中手写代码。
/// </summary>
/// <remarks>
/// <para>
/// 这些测试验证 Part Visual Runtime 闭环：
/// <list type="bullet">
/// <item>Host.TryInitialize 成功后，PartVisualDiscovery 已填充。</item>
/// <item>Storage 中的 Width/Height/X/Y 自动应用到发现的 FrameworkElement。</item>
/// <item>Missing/Duplicate Visual 产生诊断，但不阻止 Host 初始化。</item>
/// <item>TeamCardControl 不再包含手写几何读取代码。</item>
/// </list>
/// </para>
/// <para>
/// 涉及 WPF 视觉树的操作在 STA 线程上执行，遵循 <see cref="WpfTestThread"/> 规范。
/// </para>
/// </remarks>
public class FrontedV3PartVisualRuntimeBinderTest
{
    // -------------------------------------------------------------------
    // 1. PluginPart_StoredSizeIsAppliedToVisual
    // -------------------------------------------------------------------

    /// <summary>
    /// Host 初始化成功后，Part Storage 中的 Width/Height 必须自动应用到
    /// 通过 <c>fronted:FrontedV3.PartId</c> 标记的 FrameworkElement，
    /// 插件控件无需在 OnInitializeFrontedV3 中手写读取代码。
    /// </summary>
    [Fact]
    public void PluginPart_StoredSizeIsAppliedToVisual()
    {
        WpfTestThread.Run(() =>
        {
            var config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:test.host/PartControl"
            };
            config.ExtensionData["PartWidth"] = JsonSerializer.SerializeToElement(123D);
            config.ExtensionData["PartHeight"] = JsonSerializer.SerializeToElement(87D);

            var host = CreateHost<PartControlWithSizeStorage>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            Assert.NotNull(host.Control);
            var partVisual = Assert.IsType<PartControlWithSizeStorage>(host.Control);
            Assert.Equal(123, partVisual.LogoVisual.Width);
            Assert.Equal(87, partVisual.LogoVisual.Height);
        });
    }

    // -------------------------------------------------------------------
    // 2. PluginPart_StoredPositionIsAppliedToVisual
    // -------------------------------------------------------------------

    /// <summary>
    /// Host 初始化成功后，Part Storage 中的 X/Y 必须通过 Canvas.Left/Top 附加属性
    /// 应用到发现的 FrameworkElement。
    /// </summary>
    [Fact]
    public void PluginPart_StoredPositionIsAppliedToVisual()
    {
        WpfTestThread.Run(() =>
        {
            var config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:test.host/PartControl"
            };
            config.ExtensionData["PartX"] = JsonSerializer.SerializeToElement(45D);
            config.ExtensionData["PartY"] = JsonSerializer.SerializeToElement(67D);

            var host = CreateHost<PartControlWithPositionStorage>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            Assert.NotNull(host.Control);
            var partVisual = Assert.IsType<PartControlWithPositionStorage>(host.Control);
            Assert.Equal(45, Canvas.GetLeft(partVisual.LogoVisual));
            Assert.Equal(67, Canvas.GetTop(partVisual.LogoVisual));
        });
    }

    // -------------------------------------------------------------------
    // 3. PluginPart_MissingVisualProducesHostDiagnostic
    // -------------------------------------------------------------------

    /// <summary>
    /// 控件声明了 Part 但 XAML 中未标记对应 Visual 时，Host 必须仍初始化成功，
    /// 并通过 <see cref="FrontedV3ControlHost.PartVisualDiscovery"/> 输出 Missing Visual 诊断。
    /// </summary>
    [Fact]
    public void PluginPart_MissingVisualProducesHostDiagnostic()
    {
        WpfTestThread.Run(() =>
        {
            var config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:test.host/PartControl"
            };

            var host = CreateHost<PartControlWithMissingVisual>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            Assert.NotNull(host.PartVisualDiscovery);
            Assert.True(host.PartVisualDiscovery!.HasDiagnostics);
            Assert.Contains(host.PartVisualDiscovery.Diagnostics, d =>
                d.PartId == "Logo" &&
                d.Severity == FrontedV3PartVisualDiagnosticSeverity.Warning &&
                d.Message.Contains("no visual", StringComparison.OrdinalIgnoreCase));
        });
    }

    // -------------------------------------------------------------------
    // 4. PluginPart_DuplicateVisualProducesHostDiagnostic
    // -------------------------------------------------------------------

    /// <summary>
    /// 多个 Visual 映射到同一 PartId 时，Host 必须仍初始化成功，
    /// 并通过 <see cref="FrontedV3ControlHost.PartVisualDiscovery"/> 输出 Duplicate Visual 诊断。
    /// </summary>
    [Fact]
    public void PluginPart_DuplicateVisualProducesHostDiagnostic()
    {
        WpfTestThread.Run(() =>
        {
            var config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:test.host/PartControl"
            };

            var host = CreateHost<PartControlWithDuplicateVisual>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            Assert.NotNull(host.PartVisualDiscovery);
            Assert.True(host.PartVisualDiscovery!.HasDiagnostics);
            Assert.Contains(host.PartVisualDiscovery.Diagnostics, d =>
                d.PartId == "Logo" &&
                d.Severity == FrontedV3PartVisualDiagnosticSeverity.Warning &&
                d.Message.Contains("Duplicate", StringComparison.Ordinal));
        });
    }

    // -------------------------------------------------------------------
    // 5. PluginPart_DoesNotRequirePluginManualGeometryCode
    // -------------------------------------------------------------------

    /// <summary>
    /// TeamCardControl 不得重写 <see cref="FrontedV3ControlBase.OnInitializeFrontedV3"/>
    /// 来手写 Part 几何读取代码；Part 几何由框架通过
    /// <see cref="FrontedV3PartVisualRuntimeBinder"/> 统一接管。
    /// </summary>
    [Fact]
    public void PluginPart_DoesNotRequirePluginManualGeometryCode()
    {
        var onInitializeMethod = typeof(neo_bpsys_wpf.ExamplePlugin.TeamCardControl)
            .GetMethod(
                "OnInitializeFrontedV3",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(FrontedV3ControlContext) },
                modifiers: null);

        // TeamCardControl 不得重写 OnInitializeFrontedV3；如果重写，必须不是手写几何读取代码。
        // 检查方式：方法不应声明在 TeamCardControl 自身（应继承基类的空实现）。
        if (onInitializeMethod is not null
            && onInitializeMethod.DeclaringType == typeof(neo_bpsys_wpf.ExamplePlugin.TeamCardControl))
        {
            // 如果重写了，方法体应不引用 LogoWidth/LogoHeight/ExtensionData。
            // 这里通过反射验证方法存在但无手写几何代码——更严格的做法是 Roslyn 分析，
            // 当前测试通过断言"方法不属于 TeamCardControl"来强制约束。
            Assert.True(false,
                "TeamCardControl should not override OnInitializeFrontedV3; " +
                "Part geometry is now applied by FrontedV3PartVisualRuntimeBinder.");
        }

        // TeamCardControl 必须有 LogoPart 声明字段。
        var logoPartField = typeof(neo_bpsys_wpf.ExamplePlugin.TeamCardControl)
            .GetField("LogoPart", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(logoPartField);
        Assert.Equal(typeof(FrontedV3Part), logoPartField!.FieldType);
    }

    // -------------------------------------------------------------------
    // 6. PluginPart_StorageNullPreservesXamlDefaults
    // -------------------------------------------------------------------

    /// <summary>
    /// Part Storage 中无值时（如新建控件未设置过几何），绑定器不得覆盖 XAML 中声明的默认 Width/Height。
    /// </summary>
    [Fact]
    public void PluginPart_StorageNullPreservesXamlDefaults()
    {
        WpfTestThread.Run(() =>
        {
            var config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:test.host/PartControl"
            };
            // 不设置 PartWidth/PartHeight

            var host = CreateHost<PartControlWithSizeStorage>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            Assert.NotNull(host.Control);
            var partVisual = Assert.IsType<PartControlWithSizeStorage>(host.Control);
            // XAML 中声明的默认 Width=40, Height=40 不被覆盖。
            Assert.Equal(40, partVisual.LogoVisual.Width);
            Assert.Equal(40, partVisual.LogoVisual.Height);
        });
    }

    // -------------------------------------------------------------------
    // 7. PluginPart_HostWithoutFixedPartsSkipsBinder
    // -------------------------------------------------------------------

    /// <summary>
    /// 控件无 FixedParts 声明时，Host 不得调用 Binder，<see cref="FrontedV3ControlHost.PartVisualDiscovery"/> 保持 null。
    /// </summary>
    [Fact]
    public void PluginPart_HostWithoutFixedPartsSkipsBinder()
    {
        WpfTestThread.Run(() =>
        {
            var config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:test.host/Simple"
            };

            var host = CreateHost<WorkingV3Control>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));
            Assert.Null(host.PartVisualDiscovery);
        });
    }

    // -------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------

    private static FrontedV3ControlHost CreateHost<TControl>(
        FrontedControlConfigBase config,
        bool isDesignerPreview = false)
        where TControl : FrontedV3ControlBase, new()
    {
        var registration = CreateRegistration<TControl>();
        return new FrontedV3ControlHost(registration, config, isDesignerPreview);
    }

    private static FrontedV3ControlRegistration CreateRegistration<TControl>()
        where TControl : FrontedV3ControlBase
    {
        var parts = FrontedV3Part.Discover(typeof(TControl));
        return new FrontedV3ControlRegistration
        {
            CanonicalControlType = "plugin:test.host/PartControl",
            LocalControlId = "PartControl",
            PackageId = "test.host",
            IsBuiltIn = false,
            ControlType = typeof(TControl),
            ConfigType = typeof(PluginFrontedControlConfig),
            Properties = Array.Empty<FrontedV3PropertyDefinition>(),
            CreateDefaultConfig = () => new PluginFrontedControlConfig
            {
                ControlType = "plugin:test.host/PartControl"
            },
            FixedParts = parts
        };
    }

    private static FrontedV3ControlContext CreateContext(
        FrontedControlConfigBase config,
        bool isDesignerPreview = false)
    {
        return new FrontedV3ControlContext
        {
            Services = new ServiceCollection().BuildServiceProvider(),
            SharedDataService = Mock.Of<ISharedDataService>(),
            ResourceResolver = Mock.Of<IFrontedResourceResolver>(),
            WindowId = "TestWindow",
            CanvasName = "BaseCanvas",
            Config = config,
            Options = FrontedV3OptionsView.Create(config, Array.Empty<FrontedV3PropertyDefinition>()),
            IsDesignerPreview = isDesignerPreview,
            Logger = NullLogger.Instance
        };
    }
}

// ===========================================================================
// 测试用 v3 控件类型
// ===========================================================================

// 注意：WorkingV3Control 已在 FrontedV3ControlHostTest.cs 中定义，此处直接复用。

/// <summary>
/// 声明 Logo Part（仅 Resize 能力，Width/Height 存储）的测试控件，
/// XAML 中通过附加属性标记 LogoImage 作为 Part Visual。
/// </summary>
[FrontedV3Control("PartControlWithSizeStorage")]
public sealed class PartControlWithSizeStorage : FrontedV3ControlBase
{
    /// <summary>
    /// Logo Part 声明，宽高存储到 ExtensionData。
    /// </summary>
    public static readonly FrontedV3Part LogoPart =
        FrontedV3Part.Register<PartControlWithSizeStorage>("Logo")
            .WithSize(
                FrontedV3Storage.ExtensionData("PartWidth"),
                FrontedV3Storage.ExtensionData("PartHeight"))
            .WithCapabilities(FrontedV3PartCapabilities.Resize);

    /// <summary>
    /// 获取 Logo Visual。
    /// </summary>
    public Image LogoVisual { get; } = new()
    {
        Width = 40,
        Height = 40
    };

    /// <summary>
    /// 初始化控件视觉树，将 LogoVisual 标记为 PartId="Logo"。
    /// </summary>
    public PartControlWithSizeStorage()
    {
        FrontedV3.SetPartId(LogoVisual, "Logo");
        Content = new Border
        {
            Child = LogoVisual
        };
    }
}

/// <summary>
/// 声明 Logo Part（MoveAndResize 能力，X/Y 存储）的测试控件。
/// </summary>
[FrontedV3Control("PartControlWithPositionStorage")]
public sealed class PartControlWithPositionStorage : FrontedV3ControlBase
{
    /// <summary>
    /// Logo Part 声明，X/Y 存储到 ExtensionData。
    /// </summary>
    public static readonly FrontedV3Part LogoPart =
        FrontedV3Part.Register<PartControlWithPositionStorage>("Logo")
            .WithPosition(
                FrontedV3Storage.ExtensionData("PartX"),
                FrontedV3Storage.ExtensionData("PartY"))
            .WithCapabilities(FrontedV3PartCapabilities.MoveAndResize);

    /// <summary>
    /// 获取 Logo Visual。
    /// </summary>
    public Border LogoVisual { get; } = new()
    {
        Width = 30,
        Height = 30
    };

    /// <summary>
    /// 初始化控件视觉树，将 LogoVisual 标记为 PartId="Logo"。
    /// </summary>
    public PartControlWithPositionStorage()
    {
        FrontedV3.SetPartId(LogoVisual, "Logo");
        Content = new Border
        {
            Child = LogoVisual
        };
    }
}

/// <summary>
/// 声明 Logo Part 但不在视觉树中标记任何 Visual 的测试控件，用于验证 Missing Visual 诊断。
/// </summary>
[FrontedV3Control("PartControlWithMissingVisual")]
public sealed class PartControlWithMissingVisual : FrontedV3ControlBase
{
    /// <summary>
    /// Logo Part 声明，但 XAML 中不标记对应 Visual。
    /// </summary>
    public static readonly FrontedV3Part LogoPart =
        FrontedV3Part.Register<PartControlWithMissingVisual>("Logo")
            .WithSize(
                FrontedV3Storage.ExtensionData("PartWidth"),
                FrontedV3Storage.ExtensionData("PartHeight"))
            .WithCapabilities(FrontedV3PartCapabilities.Resize);

    /// <summary>
    /// 初始化控件视觉树，不含任何 Part Visual 标记。
    /// </summary>
    public PartControlWithMissingVisual()
    {
        Content = new Border
        {
            Background = System.Windows.Media.Brushes.Transparent
        };
    }
}

/// <summary>
/// 声明 Logo Part 并在视觉树中标记两个 Visual 为同一 PartId 的测试控件，
/// 用于验证 Duplicate Visual 诊断。
/// </summary>
[FrontedV3Control("PartControlWithDuplicateVisual")]
public sealed class PartControlWithDuplicateVisual : FrontedV3ControlBase
{
    /// <summary>
    /// Logo Part 声明。
    /// </summary>
    public static readonly FrontedV3Part LogoPart =
        FrontedV3Part.Register<PartControlWithDuplicateVisual>("Logo")
            .WithSize(
                FrontedV3Storage.ExtensionData("PartWidth"),
                FrontedV3Storage.ExtensionData("PartHeight"))
            .WithCapabilities(FrontedV3PartCapabilities.Resize);

    /// <summary>
    /// 初始化控件视觉树，标记两个 Visual 为 PartId="Logo"。
    /// </summary>
    public PartControlWithDuplicateVisual()
    {
        var first = new Image();
        var second = new Image();
        FrontedV3.SetPartId(first, "Logo");
        FrontedV3.SetPartId(second, "Logo");
        Content = new StackPanel();
        ((StackPanel)Content).Children.Add(first);
        ((StackPanel)Content).Children.Add(second);
    }
}
