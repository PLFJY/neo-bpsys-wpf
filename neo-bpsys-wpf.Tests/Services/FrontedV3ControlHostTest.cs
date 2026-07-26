using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Options;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Phase 2 SubTask 3.6 测试：覆盖 <see cref="FrontedV3ControlHost"/> 接管根控件布局的全部场景。
/// </summary>
/// <remarks>
/// <para>
/// 这些测试验证 Host 是新 v3 控件路径中唯一的根布局 Owner：Host 从 Config 读取
/// Left/Top/Width/Height/ZIndex/Visibility/GaussianBlur/BehaviorGuid，一次性应用到自身；
/// 包装的 Control 不设置自己的 Canvas 坐标。
/// </para>
/// <para>
/// 失败场景验证错误边界：构造或初始化失败时显示占位、保留原 Config、不写默认值。
/// 所有 WPF 操作在 STA 线程上执行，遵循 <see cref="WpfTestThread"/> 规范。
/// </para>
/// <para>
/// 这些是架构契约测试（验证 Host 拥有根布局所有权），不是视觉样式断言：
/// 断言的对象是 Host 与 Config 之间的数据流契约，而非展示细节。
/// </para>
/// </remarks>
public class FrontedV3ControlHostTest
{
    // -------------------------------------------------------------------
    // 1. HostAppliesRootPosition
    // -------------------------------------------------------------------

    /// <summary>
    /// Host 初始化成功后必须从 Config 读取 Left/Top 并应用到自身 Canvas 附加属性。
    /// </summary>
    [Fact]
    public void HostAppliesRootPosition()
    {
        WpfTestThread.Run(() =>
        {
            var config = CreateConfig(left: 120, top: 80);
            var host = CreateHost<WorkingV3Control>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            Assert.Equal(120, Canvas.GetLeft(host));
            Assert.Equal(80, Canvas.GetTop(host));
        });
    }

    // -------------------------------------------------------------------
    // 2. HostAppliesSizeAndZIndex
    // -------------------------------------------------------------------

    /// <summary>
    /// Host 初始化成功后必须从 Config 读取 Width/Height/ZIndex 并应用到自身。
    /// </summary>
    [Fact]
    public void HostAppliesSizeAndZIndex()
    {
        WpfTestThread.Run(() =>
        {
            var config = CreateConfig(width: 300, height: 150, zIndex: 7);
            var host = CreateHost<WorkingV3Control>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            Assert.Equal(300, host.Width);
            Assert.Equal(150, host.Height);
            Assert.Equal(7, Panel.GetZIndex(host));
        });
    }

    /// <summary>
    /// Config 的 Width/Height 为 null 时，Host 必须将自身 Width/Height 设为 NaN（自适应）。
    /// </summary>
    [Fact]
    public void HostAppliesAdaptiveSizeWhenConfigHasNoSize()
    {
        WpfTestThread.Run(() =>
        {
            var config = CreateConfig(width: null, height: null);
            var host = CreateHost<WorkingV3Control>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            Assert.True(double.IsNaN(host.Width));
            Assert.True(double.IsNaN(host.Height));
        });
    }

    // -------------------------------------------------------------------
    // 3. ControlDoesNotNeedCanvasCoordinates
    // -------------------------------------------------------------------

    /// <summary>
    /// Host 初始化成功后，包装的 Control 不得设置自己的 Canvas 坐标，
    /// 根布局所有权归 Host。
    /// </summary>
    [Fact]
    public void ControlDoesNotNeedCanvasCoordinates()
    {
        WpfTestThread.Run(() =>
        {
            var config = CreateConfig(left: 100, top: 50);
            var host = CreateHost<WorkingV3Control>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            Assert.NotNull(host.Control);
            // Control 不拥有 Canvas 坐标（NaN 表示未设置 Canvas.Left/Top）。
            Assert.True(double.IsNaN(Canvas.GetLeft(host.Control!)));
            Assert.True(double.IsNaN(Canvas.GetTop(host.Control!)));
            // Host 才是 Canvas 坐标的 Owner。
            Assert.Equal(100, Canvas.GetLeft(host));
            Assert.Equal(50, Canvas.GetTop(host));
        });
    }

    // -------------------------------------------------------------------
    // 4. HostAppliesVisibility
    // -------------------------------------------------------------------

    /// <summary>
    /// Host 必须从 Config 读取 Visibility 并映射到自身 <see cref="Visibility"/>。
    /// </summary>
    [Theory]
    [InlineData(FrontedControlVisibility.Visible, Visibility.Visible)]
    [InlineData(FrontedControlVisibility.Hidden, Visibility.Hidden)]
    [InlineData(FrontedControlVisibility.Collapsed, Visibility.Collapsed)]
    public void HostAppliesVisibility(FrontedControlVisibility configVisibility, Visibility expectedWpfVisibility)
    {
        WpfTestThread.Run(() =>
        {
            var config = CreateConfig(visibility: configVisibility);
            var host = CreateHost<WorkingV3Control>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            Assert.Equal(expectedWpfVisibility, host.Visibility);
        });
    }

    // -------------------------------------------------------------------
    // 5. HostAppliesBlur
    // -------------------------------------------------------------------

    /// <summary>
    /// Config 启用 GaussianBlur 时，Host 必须在自身应用 BlurEffect；
    /// 未启用或半径非正时 Host.Effect 必须为 null。
    /// </summary>
    [Fact]
    public void HostAppliesBlur()
    {
        WpfTestThread.Run(() =>
        {
            // 启用场景
            var enabledConfig = CreateConfig(isGaussianBlurEnabled: true, gaussianBlurRadius: 12);
            var enabledHost = CreateHost<WorkingV3Control>(enabledConfig);
            Assert.True(enabledHost.TryInitialize(CreateContext(enabledConfig)));

            var blur = Assert.IsType<BlurEffect>(enabledHost.Effect);
            Assert.Equal(12, blur.Radius);

            // 未启用场景
            var disabledConfig = CreateConfig(isGaussianBlurEnabled: false, gaussianBlurRadius: 12);
            var disabledHost = CreateHost<WorkingV3Control>(disabledConfig);
            Assert.True(disabledHost.TryInitialize(CreateContext(disabledConfig)));
            Assert.Null(disabledHost.Effect);

            // 半径非正场景
            var zeroRadiusConfig = CreateConfig(isGaussianBlurEnabled: true, gaussianBlurRadius: 0);
            var zeroRadiusHost = CreateHost<WorkingV3Control>(zeroRadiusConfig);
            Assert.True(zeroRadiusHost.TryInitialize(CreateContext(zeroRadiusConfig)));
            Assert.Null(zeroRadiusHost.Effect);
        });
    }

    // -------------------------------------------------------------------
    // 6. HostPreservesBehaviorGuid
    // -------------------------------------------------------------------

    /// <summary>
    /// Host 必须从 Config 读取 BehaviorGuid 并设置为自身附加属性，
    /// 同时标记 IsGeneratedControl=true。
    /// </summary>
    [Fact]
    public void HostPreservesBehaviorGuid()
    {
        WpfTestThread.Run(() =>
        {
            var guid = Guid.NewGuid();
            var config = CreateConfig(behaviorGuid: guid);
            var host = CreateHost<WorkingV3Control>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            Assert.Equal(guid, FrontedRendererProperties.GetBehaviorGuid(host));
            Assert.True(FrontedRendererProperties.GetIsGeneratedControl(host));
        });
    }

    // -------------------------------------------------------------------
    // 7. ConstructorFailureDoesNotCrashWindow
    // -------------------------------------------------------------------

    /// <summary>
    /// Control 构造函数抛出异常时，<see cref="FrontedV3ControlHost.TryInitialize"/>
    /// 必须返回 false、切换到错误占位、不向调用方抛出异常。
    /// </summary>
    [Fact]
    public void ConstructorFailureDoesNotCrashWindow()
    {
        WpfTestThread.Run(() =>
        {
            var config = CreateConfig();
            var host = CreateHost<ConstructorFailingV3Control>(config);

            var result = host.TryInitialize(CreateContext(config));

            Assert.False(result);
            Assert.True(host.HasError);
            Assert.Null(host.Control);
            // 反射包装的 TargetInvocationException 必须被解包为真实异常。
            Assert.IsType<InvalidOperationException>(host.InitializationError);
            // 错误占位必须是 Border（不是 Control）。
            Assert.IsType<Border>(host.Child);
        });
    }

    // -------------------------------------------------------------------
    // 8. InitializationFailureShowsDesignerPlaceholder
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ControlBase.OnInitializeFrontedV3"/> 抛出异常时，
    /// Designer 预览模式必须显示错误占位，保留原 Config，不写默认值。
    /// </summary>
    [Fact]
    public void InitializationFailureShowsDesignerPlaceholder()
    {
        WpfTestThread.Run(() =>
        {
            var config = CreateConfig(left: 200, top: 100, width: 80, height: 40);
            var host = CreateHost<InitFailingV3Control>(config, isDesignerPreview: true);

            var result = host.TryInitialize(CreateContext(config, isDesignerPreview: true));

            Assert.False(result);
            Assert.True(host.HasError);
            Assert.Null(host.Control);
            Assert.IsType<InvalidOperationException>(host.InitializationError);

            // Designer 错误占位必须可见并显示 Designer 标识。
            var placeholder = Assert.IsType<Border>(host.Child);
            Assert.Equal(Visibility.Visible, placeholder.Visibility);
            var textBlock = Assert.IsType<TextBlock>(placeholder.Child);
            Assert.Contains("Designer", textBlock.Text, StringComparison.Ordinal);

            // 原 Config 必须保留，不写默认值。
            Assert.Equal(200, config.Left);
            Assert.Equal(100, config.Top);
            Assert.Equal(80, config.Width);
            Assert.Equal(40, config.Height);
        });
    }

    // -------------------------------------------------------------------
    // 9. MissingPluginConfigIsNotModified
    // -------------------------------------------------------------------

    /// <summary>
    /// 初始化失败时（模拟插件缺失或服务解析失败场景），
    /// 原 Config 的 ExtensionData 与根级字段必须原样保留，不写入任何默认值。
    /// </summary>
    [Fact]
    public void MissingPluginConfigIsNotModified()
    {
        WpfTestThread.Run(() =>
        {
            var config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:test.host/V3Control",
                Left = 150,
                Top = 90,
                Width = 220,
                Height = 110,
                ZIndex = 3,
                Visibility = FrontedControlVisibility.Visible,
                IsGaussianBlurEnabled = true,
                GaussianBlurRadius = 8
            };
            config.ExtensionData["TextColor"] = JsonSerializer.SerializeToElement("#FFAA0000");
            config.ExtensionData["TeamName"] = JsonSerializer.SerializeToElement("ASG");
            config.ExtensionData["FontSize"] = JsonSerializer.SerializeToElement(18);

            // 捕获初始化前的快照。
            var snapshotLeft = config.Left;
            var snapshotTop = config.Top;
            var snapshotWidth = config.Width;
            var snapshotHeight = config.Height;
            var snapshotZIndex = config.ZIndex;
            var snapshotControlType = config.ControlType;
            var snapshotVisibility = config.Visibility;
            var snapshotBlurEnabled = config.IsGaussianBlurEnabled;
            var snapshotBlurRadius = config.GaussianBlurRadius;
            var snapshotExtensionKeys = config.ExtensionData.Keys.ToList();

            var host = CreateHost<InitFailingV3Control>(config, isDesignerPreview: true);

            var result = host.TryInitialize(CreateContext(config, isDesignerPreview: true));

            // 初始化失败，Config 必须原样保留。
            Assert.False(result);
            Assert.Equal(snapshotLeft, config.Left);
            Assert.Equal(snapshotTop, config.Top);
            Assert.Equal(snapshotWidth, config.Width);
            Assert.Equal(snapshotHeight, config.Height);
            Assert.Equal(snapshotZIndex, config.ZIndex);
            Assert.Equal(snapshotControlType, config.ControlType);
            Assert.Equal(snapshotVisibility, config.Visibility);
            Assert.Equal(snapshotBlurEnabled, config.IsGaussianBlurEnabled);
            Assert.Equal(snapshotBlurRadius, config.GaussianBlurRadius);

            // ExtensionData 的 key 集合与值不变。
            Assert.Equal(snapshotExtensionKeys, config.ExtensionData.Keys.ToList());
            Assert.Equal("#FFAA0000", config.ExtensionData["TextColor"].GetString());
            Assert.Equal("ASG", config.ExtensionData["TeamName"].GetString());
            Assert.Equal(18, config.ExtensionData["FontSize"].GetInt32());
        });
    }

    // -------------------------------------------------------------------
    // 附加契约：ApplyRootLayout 可在 Config 变更后重新应用
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ControlHost.ApplyRootLayout"/> 必须从当前 Config 重新应用所有根属性，
    /// 用于 Config 被外部修改后恢复视觉一致性。
    /// </summary>
    [Fact]
    public void ApplyRootLayout_RereadsFromCurrentConfig()
    {
        WpfTestThread.Run(() =>
        {
            var config = CreateConfig(left: 10, top: 20, width: 100, height: 50, zIndex: 1);
            var host = CreateHost<WorkingV3Control>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));
            Assert.Equal(10, Canvas.GetLeft(host));

            // 外部修改 Config 后调用 ApplyRootLayout，Host 视觉必须同步。
            config.Left = 500;
            config.Top = 300;
            config.ZIndex = 9;
            host.ApplyRootLayout();

            Assert.Equal(500, Canvas.GetLeft(host));
            Assert.Equal(300, Canvas.GetTop(host));
            Assert.Equal(9, Panel.GetZIndex(host));
        });
    }

    // -------------------------------------------------------------------
    // 附加契约：GetGeometryTarget 返回绑定到 Host 与 Config 的目标
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ControlHost.GetGeometryTarget"/> 必须返回绑定到当前 Host 与 Config 的
    /// <see cref="IFrontedV3GeometryTarget"/>，MoveTo/ResizeTo 写入 Config 并同步 Host 视觉。
    /// </summary>
    [Fact]
    public void GetGeometryTarget_WritesConfigAndSyncsHost()
    {
        WpfTestThread.Run(() =>
        {
            var config = CreateConfig(left: 10, top: 20);
            var host = CreateHost<WorkingV3Control>(config);

            Assert.True(host.TryInitialize(CreateContext(config)));

            var target = host.GetGeometryTarget();
            Assert.Equal(10, target.Left);
            Assert.Equal(20, target.Top);

            target.MoveTo(100, 200);
            Assert.Equal(100, config.Left);
            Assert.Equal(200, config.Top);
            Assert.Equal(100, Canvas.GetLeft(host));
            Assert.Equal(200, Canvas.GetTop(host));

            target.ResizeTo(50, 60, 250, 120);
            Assert.Equal(50, config.Left);
            Assert.Equal(60, config.Top);
            Assert.Equal(250, config.Width);
            Assert.Equal(120, config.Height);
            Assert.Equal(250, host.Width);
            Assert.Equal(120, host.Height);
        });
    }

    // -------------------------------------------------------------------
    // 附加契约：未初始化的 Host 调用 ApplyRootLayout 也能应用 Config
    // -------------------------------------------------------------------

    /// <summary>
    /// 未附加 Control 的 Host 调用 <see cref="FrontedV3ControlHost.ApplyRootLayout"/>
    /// 也必须正确应用根属性，因为 Host 自身是根布局 Owner，与 Control 是否存在无关。
    /// </summary>
    [Fact]
    public void ApplyRootLayout_WorksBeforeAttachControl()
    {
        WpfTestThread.Run(() =>
        {
            var config = CreateConfig(left: 30, top: 40, width: 90, height: 60, zIndex: 2);
            var host = CreateHost<WorkingV3Control>(config);

            // 不调用 TryInitialize，直接 ApplyRootLayout。
            host.ApplyRootLayout();

            Assert.Equal(30, Canvas.GetLeft(host));
            Assert.Equal(40, Canvas.GetTop(host));
            Assert.Equal(2, Panel.GetZIndex(host));
            Assert.Equal(90, host.Width);
            Assert.Equal(60, host.Height);
            Assert.True(FrontedRendererProperties.GetIsGeneratedControl(host));
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
        return new FrontedV3ControlRegistration
        {
            CanonicalControlType = "plugin:test.host/V3Control",
            LocalControlId = "V3Control",
            PackageId = "test.host",
            IsBuiltIn = false,
            ControlType = typeof(TControl),
            ConfigType = typeof(PluginFrontedControlConfig),
            Properties = Array.Empty<FrontedV3PropertyDefinition>(),
            CreateDefaultConfig = () => new PluginFrontedControlConfig
            {
                ControlType = "plugin:test.host/V3Control"
            }
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

    private static PluginFrontedControlConfig CreateConfig(
        double left = 0,
        double top = 0,
        double? width = null,
        double? height = null,
        int zIndex = 0,
        FrontedControlVisibility visibility = FrontedControlVisibility.Visible,
        bool isGaussianBlurEnabled = false,
        double gaussianBlurRadius = 0,
        Guid? behaviorGuid = null)
    {
        var config = new PluginFrontedControlConfig
        {
            ControlType = "plugin:test.host/V3Control"
        };
        config.Left = left;
        config.Top = top;
        config.Width = width;
        config.Height = height;
        config.ZIndex = zIndex;
        config.Visibility = visibility;
        config.IsGaussianBlurEnabled = isGaussianBlurEnabled;
        config.GaussianBlurRadius = gaussianBlurRadius;
        if (behaviorGuid.HasValue)
        {
            config.BehaviorGuid = behaviorGuid.Value;
        }

        return config;
    }
}

// ===========================================================================
// 测试用 v3 控件类型
// ===========================================================================

/// <summary>
/// 正常工作的 v3 测试控件，不设置任何 Canvas 坐标，验证 Host 接管根布局。
/// </summary>
[FrontedV3Control("WorkingControl")]
public sealed class WorkingV3Control : FrontedV3ControlBase
{
    /// <summary>
    /// 初始化控件视觉树，不设置 Canvas 坐标。
    /// </summary>
    public WorkingV3Control()
    {
        Content = new Border
        {
            Background = System.Windows.Media.Brushes.Transparent
        };
    }
}

/// <summary>
/// 构造函数抛出异常的 v3 测试控件，用于验证错误边界。
/// </summary>
[FrontedV3Control("ConstructorFailingControl")]
public sealed class ConstructorFailingV3Control : FrontedV3ControlBase
{
    /// <summary>
    /// 初始化控件时抛出异常，模拟 XAML InitializeComponent 或构造失败。
    /// </summary>
    public ConstructorFailingV3Control()
    {
        throw new InvalidOperationException("Constructor failure for test.");
    }
}

/// <summary>
/// <see cref="FrontedV3ControlBase.OnInitializeFrontedV3"/> 抛出异常的 v3 测试控件，
/// 用于验证初始化失败时的错误占位与 Config 保留。
/// </summary>
[FrontedV3Control("InitFailingControl")]
public sealed class InitFailingV3Control : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        throw new InvalidOperationException("Initialization failure for test.");
    }
}
