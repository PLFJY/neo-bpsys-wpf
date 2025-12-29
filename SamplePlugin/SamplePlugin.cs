using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Plugins;
using neo_bpsys_wpf.Core.Plugins.Abstractions;
using neo_bpsys_wpf.Core.Plugins.Events;
using neo_bpsys_wpf.Core.Plugins.Services;

namespace SamplePlugin;

/// <summary>
/// 示例插件 - 展示插件系统的所有可用功能
/// </summary>
public class SamplePlugin : PluginBase
{
    private IServiceProvider? _serviceProvider;
    private IPluginLogger? _logger;
    private IDisposable? _themeSubscription;
    private IDisposable? _dataSubscription;

    /// <inheritdoc/>
    public override IPluginMetadata Metadata { get; } = new PluginMetadata
    {
        Id = "com.sample.plugin",
        Name = "示例插件",
        Version = new Version(1, 0, 0),
        Author = "Plugin Developer",
        Description = "这是一个功能完整的示例插件，展示了插件系统的所有可用功能",
        Dependencies = [],
        MinimumHostVersion = new Version(1, 0, 0),
        HomepageUrl = "https://github.com/example/sample-plugin"
    };

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册插件自己的服务
        services.AddSingleton<ISampleService, SampleService>();
    }

    /// <inheritdoc/>
    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(serviceProvider, cancellationToken);

        _serviceProvider = serviceProvider;
        
        // 获取日志服务
        var loggerFactory = serviceProvider.GetService<IPluginLoggerFactory>();
        _logger = loggerFactory?.CreateLogger(Metadata.Id);

        _logger?.Info("示例插件正在初始化...");

        // 注册UI扩展点
        var uiExtensionService = serviceProvider.GetService<IUIExtensionService>();
        var configService = serviceProvider.GetService<IPluginConfigurationService>();
        var hostService = serviceProvider.GetService<IHostApplicationService>();
        
        if (uiExtensionService != null)
        {
            // 1. 注册导航页面扩展
            _logger?.Info("注册导航页面扩展...");
            uiExtensionService.RegisterExtension(new SampleNavigationPageExtension());
            
            // 2. 注册设置页面扩展
            if (configService != null && hostService != null)
            {
                _logger?.Info("注册设置页面扩展...");
                uiExtensionService.RegisterExtension(new SampleSettingsExtension(configService, hostService));
            }
            
            // 3. 注册前台窗口扩展
            if (configService != null && hostService != null)
            {
                _logger?.Info("注册前台窗口扩展...");
                uiExtensionService.RegisterExtension(new SampleFrontWindowExtension(configService, hostService));
            }
        }

        // 订阅应用事件
        var eventBus = serviceProvider.GetService<IPluginEventBus>();
        if (eventBus != null)
        {
            // 订阅主题变更事件
            _logger?.Info("订阅主题变更事件...");
            _themeSubscription = eventBus.Subscribe<ThemeChangedEvent>(OnThemeChanged);
            
            // 订阅导航事件
            _dataSubscription = eventBus.Subscribe<NavigationEvent>(OnNavigationEvent);
        }

        _logger?.Info("示例插件初始化完成");
    }

    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);
        
        _logger?.Info("示例插件已启动");
        
        // 显示启动通知
        var hostService = _serviceProvider?.GetService<IHostApplicationService>();
        hostService?.ShowNotification(
            "插件已加载",
            $"{Metadata.Name} v{Metadata.Version} 已成功加载",
            NotificationType.Success);
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger?.Info("示例插件正在停止...");
        
        // 取消所有事件订阅
        _themeSubscription?.Dispose();
        _dataSubscription?.Dispose();
        
        _logger?.Info("示例插件已停止");
        await base.StopAsync(cancellationToken);
    }

    private void OnThemeChanged(ThemeChangedEvent e)
    {
        _logger?.Info($"主题已变更: {e.NewTheme}");
        
        // 可以根据主题变更更新插件UI
        var hostService = _serviceProvider?.GetService<IHostApplicationService>();
        hostService?.ShowNotification(
            "主题已变更",
            $"应用主题已切换到 {e.NewTheme}",
            NotificationType.Information);
    }
    
    private void OnNavigationEvent(NavigationEvent evt)
    {
        _logger?.Debug($"收到导航事件: {evt.TargetPageType?.Name}");
    }
}
