using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Plugins;

namespace SamplePlugin;

/// <summary>
/// 示例插件 - 演示如何创建插件
/// Sample plugin - Demonstrates how to create a plugin
/// </summary>
public class SamplePlugin : UIPluginBase
{
    private ILogger? _logger;

    public override string Id => "com.example.sampleplugin";

    public override string Name => "示例插件";

    public override string Description => "这是一个演示如何创建插件的示例插件，包含一个简单的UI页面。";

    public override Version Version => new Version(1, 0, 0);

    public override string Author => "Plugin Developer";

    protected override Task OnInitializeAsync(IPluginContext context)
    {
        _logger = context.LoggerFactory.CreateLogger<SamplePlugin>();
        _logger.LogInformation("Sample plugin initialized");
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync()
    {
        _logger?.LogInformation("Sample plugin started");
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync()
    {
        _logger?.LogInformation("Sample plugin stopped");
        return Task.CompletedTask;
    }

    protected override void OnConfigureServices(IServiceCollection services)
    {
        // 注册插件的服务
        services.AddSingleton<SamplePageViewModel>();
    }

    protected override IEnumerable<PluginPageDescriptor> OnGetPages()
    {
        return new[]
        {
            new PluginPageDescriptor
            {
                PageType = typeof(SamplePage),
                ViewModelType = typeof(SamplePageViewModel),
                Title = "示例页面",
                Icon = "AppGeneric",
                Route = "sample",
                ShowInNavigation = true,
                Priority = 200
            }
        };
    }

    protected override IEnumerable<PluginControlDescriptor> OnGetControls()
    {
        return new[]
        {
            new PluginControlDescriptor
            {
                ControlType = typeof(SampleControl),
                Name = "示例控件",
                Description = "一个示例自定义控件",
                Category = "示例"
            }
        };
    }
}

/// <summary>
/// 示例页面视图模型
/// Sample page view model
/// </summary>
public class SamplePageViewModel
{
    public string WelcomeMessage { get; } = "欢迎使用示例插件！";
    public string PluginInfo { get; } = "这是一个通过插件系统加载的页面。";
}

/// <summary>
/// 示例页面
/// Sample page
/// </summary>
public class SamplePage : Page
{
    public SamplePage()
    {
        var stackPanel = new StackPanel
        {
            Margin = new System.Windows.Thickness(20)
        };

        var title = new TextBlock
        {
            Text = "示例插件页面",
            FontSize = 28,
            FontWeight = System.Windows.FontWeights.Bold,
            Margin = new System.Windows.Thickness(0, 0, 0, 20)
        };

        var description = new TextBlock
        {
            Text = "这是一个由插件提供的页面示例。插件可以向主应用程序添加自定义页面、控件和功能。",
            FontSize = 14,
            TextWrapping = System.Windows.TextWrapping.Wrap,
            Margin = new System.Windows.Thickness(0, 0, 0, 20)
        };

        var info = new TextBlock
        {
            Text = "插件功能:\n• 自定义UI页面\n• 自定义控件\n• 服务注入\n• 事件通信\n• 配置管理",
            FontSize = 14,
            Margin = new System.Windows.Thickness(0, 0, 0, 20)
        };

        stackPanel.Children.Add(title);
        stackPanel.Children.Add(description);
        stackPanel.Children.Add(info);

        Content = stackPanel;
    }
}

/// <summary>
/// 示例自定义控件
/// Sample custom control
/// </summary>
public class SampleControl : Control
{
    static SampleControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SampleControl),
            new System.Windows.FrameworkPropertyMetadata(typeof(SampleControl)));
    }

    public SampleControl()
    {
        // 控件初始化代码
    }
}
