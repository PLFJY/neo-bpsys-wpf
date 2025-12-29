using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Plugins;
using neo_bpsys_wpf.Core.Plugins.Commands;
using neo_bpsys_wpf.Core.Plugins.Events;
using neo_bpsys_wpf.Core.Plugins.UI;
using System.Windows.Input;

namespace SamplePlugin;

/// <summary>
/// 示例插件 - 展示如何创建一个基本的插件
/// </summary>
public sealed class SamplePlugin : EnhancedPluginBase
{
    private SamplePluginSettings? _settings;
    private SamplePageViewModel? _viewModel;

    /// <inheritdoc/>
    public override string Id => "com.example.sampleplugin";

    /// <inheritdoc/>
    public override string Name => "示例插件";

    /// <inheritdoc/>
    public override Version Version => new(1, 0, 0);

    /// <inheritdoc/>
    public override string Description => "这是一个示例插件，展示了插件系统的基本功能。";

    /// <inheritdoc/>
    public override string Author => "Plugin Developer";

    /// <inheritdoc/>
    public override Task InitializeAsync(IServiceProvider serviceProvider)
    {
        return base.InitializeAsync(serviceProvider);
    }

    /// <inheritdoc/>
    public override async Task EnableAsync()
    {
        await base.EnableAsync();

        // 加载设置
        _settings = GetPluginSettings<SamplePluginSettings>();

        // 创建ViewModel
        _viewModel = new SamplePageViewModel
        {
            Counter = _settings.Counter,
            CounterChangedCallback = counter =>
            {
                if (_settings != null)
                {
                    _settings.Counter = counter;
                    SavePluginSettings(_settings);
                }
            }
        };

        // 注册UI组件
        RegisterUIComponents();

        // 注册命令
        RegisterPluginCommands();

        // 订阅事件
        SubscribeToEvents();

        // 显示欢迎消息
        if (_settings.ShowWelcomeMessage)
        {
            ShowNotification(Name, _settings.CustomMessage, NotificationSeverity.Information);
        }
    }

    /// <inheritdoc/>
    public override async Task DisableAsync()
    {
        // 保存设置
        if (_settings != null)
        {
            SavePluginSettings(_settings);
        }

        _viewModel = null;
        _settings = null;

        await base.DisableAsync();
    }

    /// <summary>
    /// 注册UI组件
    /// </summary>
    private void RegisterUIComponents()
    {
        // 注册导航页面
        RegisterNavigationPage(new NavigationPageRegistration
        {
            Id = $"{Id}.page",
            DisplayName = "示例插件",
            PageType = typeof(SamplePage),
            ViewModelType = typeof(SamplePageViewModel),
            Priority = 1000,
            ShowInNavigation = true
        });

        // 注册工具栏组件
        RegisterComponent(new UIComponentRegistration
        {
            Id = $"{Id}.toolbar",
            DisplayName = "示例工具栏按钮",
            ExtensionPoint = UIExtensionPoint.MainToolbar,
            ComponentFactory = sp =>
            {
                var button = new System.Windows.Controls.Button
                {
                    Content = "SP",
                    ToolTip = "示例插件按钮",
                    Padding = new System.Windows.Thickness(10, 5, 10, 5)
                };
                button.Click += (s, e) =>
                {
                    ShowNotification("示例插件", "您点击了工具栏按钮！", NotificationSeverity.Information);
                };
                return button;
            },
            Priority = 100
        });
    }

    /// <summary>
    /// 注册命令
    /// </summary>
    private void RegisterPluginCommands()
    {
        // 注册增加计数器命令
        RegisterCommand(new CommandRegistration
        {
            Id = $"{Id}.increment",
            DisplayName = "增加计数器",
            Description = "增加示例插件的计数器值",
            Command = new RelayCommand(() =>
            {
                if (_viewModel != null)
                {
                    _viewModel.Counter++;
                    if (_settings != null)
                    {
                        _settings.Counter = _viewModel.Counter;
                        SavePluginSettings(_settings);
                    }
                }
            }),
            Group = "SamplePlugin",
            KeyGesture = new KeyGesture(Key.Add, ModifierKeys.Control | ModifierKeys.Shift)
        });

        // 注册显示消息命令
        RegisterCommand(new CommandRegistration
        {
            Id = $"{Id}.showmessage",
            DisplayName = "显示消息",
            Description = "显示示例插件的消息",
            Command = new RelayCommand(() =>
            {
                ShowNotification(Name, _settings?.CustomMessage ?? "Hello!", NotificationSeverity.Information);
            }),
            Group = "SamplePlugin"
        });
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    private void SubscribeToEvents()
    {
        // 订阅主题变更事件
        Subscribe<ThemeChangedEvent>(async e =>
        {
            await InvokeOnUIAsync(() =>
            {
                if (_viewModel != null)
                {
                    _viewModel.Message = $"主题已变更为: {e.NewTheme}";
                }
            });
        });

        // 订阅应用程序关闭事件
        Subscribe<ApplicationShuttingDownEvent>(async _ =>
        {
            // 保存设置
            if (_settings != null)
            {
                _settings.Counter = _viewModel?.Counter ?? 0;
                SavePluginSettings(_settings);
            }
            await Task.CompletedTask;
        });
    }
}
