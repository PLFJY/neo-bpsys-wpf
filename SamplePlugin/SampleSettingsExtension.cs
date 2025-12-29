using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Plugins.Services;
using neo_bpsys_wpf.Core.Plugins.UI;

namespace SamplePlugin;

/// <summary>
/// 示例设置扩展 - 展示完整的配置功能
/// </summary>
public class SampleSettingsExtension : SettingsExtensionBase
{
    private readonly IPluginConfigurationService _configService;
    private readonly IHostApplicationService _hostService;
    
    // UI 控件
    private TextBox? _greetingTextBox;
    private CheckBox? _enableFeatureCheckBox;
    private ComboBox? _themeComboBox;
    private Slider? _volumeSlider;
    private TextBlock? _volumeDisplay;

    // 配置键
    private const string PluginId = "com.sample.plugin";
    private const string GreetingKey = "greeting";
    private const string EnableFeatureKey = "enableFeature";
    private const string ThemePreferenceKey = "themePreference";
    private const string VolumeKey = "volume";

    public SampleSettingsExtension(IPluginConfigurationService configService, IHostApplicationService hostService)
    {
        _configService = configService;
        _hostService = hostService;
    }

    /// <inheritdoc/>
    public override string Id => "sample-plugin-settings";

    /// <inheritdoc/>
    public override string Title => "示例插件设置";

    /// <inheritdoc/>
    public override string? Description => "配置示例插件的各项功能";

    /// <inheritdoc/>
    public override string GroupName => "插件配置";

    /// <inheritdoc/>
    public override FrameworkElement CreateElement()
    {
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(15)
        };

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(10)
        };

        // === 基本设置区 ===
        mainPanel.Children.Add(CreateSection("基本设置", CreateBasicSettings()));

        // === 高级设置区 ===
        mainPanel.Children.Add(CreateSection("高级设置", CreateAdvancedSettings()));

        // === 按钮区 ===
        mainPanel.Children.Add(CreateButtonPanel());

        scrollViewer.Content = mainPanel;

        // 加载当前设置
        _ = LoadSettingsAsync();

        return scrollViewer;
    }

    private Border CreateSection(string title, FrameworkElement content)
    {
        var section = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 100, 100, 100)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 15)
        };

        var panel = new StackPanel();

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 15)
        };

        panel.Children.Add(titleBlock);
        panel.Children.Add(content);
        section.Child = panel;

        return section;
    }

    private FrameworkElement CreateBasicSettings()
    {
        var panel = new StackPanel();

        // 问候语设置
        var greetingLabel = new TextBlock
        {
            Text = "自定义问候语:",
            Margin = new Thickness(0, 0, 0, 5),
            FontSize = 14
        };

        _greetingTextBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 15),
            Padding = new Thickness(8),
            FontSize = 14
        };

        var greetingHint = new TextBlock
        {
            Text = "这将在插件启动时显示",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
            Margin = new Thickness(0, -10, 0, 15)
        };

        // 功能开关
        _enableFeatureCheckBox = new CheckBox
        {
            Content = "启用高级功能",
            Margin = new Thickness(0, 0, 0, 10),
            FontSize = 14
        };

        var featureHint = new TextBlock
        {
            Text = "开启后将解锁更多功能",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
            Margin = new Thickness(25, -5, 0, 15)
        };

        panel.Children.Add(greetingLabel);
        panel.Children.Add(_greetingTextBox);
        panel.Children.Add(greetingHint);
        panel.Children.Add(_enableFeatureCheckBox);
        panel.Children.Add(featureHint);

        return panel;
    }

    private FrameworkElement CreateAdvancedSettings()
    {
        var panel = new StackPanel();

        // 主题偏好
        var themeLabel = new TextBlock
        {
            Text = "主题偏好:",
            Margin = new Thickness(0, 0, 0, 5),
            FontSize = 14
        };

        _themeComboBox = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 15),
            Padding = new Thickness(8),
            FontSize = 14
        };
        _themeComboBox.Items.Add("跟随系统");
        _themeComboBox.Items.Add("明亮主题");
        _themeComboBox.Items.Add("暗黑主题");
        _themeComboBox.SelectedIndex = 0;

        // 音量设置
        var volumeLabel = new TextBlock
        {
            Text = "通知音量:",
            Margin = new Thickness(0, 0, 0, 5),
            FontSize = 14
        };

        var volumePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 15)
        };

        _volumeSlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Width = 200,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        _volumeDisplay = new TextBlock
        {
            Text = "50%",
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 50
        };

        _volumeSlider.ValueChanged += (s, e) =>
        {
            if (_volumeDisplay != null)
            {
                _volumeDisplay.Text = $"{(int)e.NewValue}%";
            }
        };

        volumePanel.Children.Add(_volumeSlider);
        volumePanel.Children.Add(_volumeDisplay);

        panel.Children.Add(themeLabel);
        panel.Children.Add(_themeComboBox);
        panel.Children.Add(volumeLabel);
        panel.Children.Add(volumePanel);

        return panel;
    }

    private FrameworkElement CreateButtonPanel()
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var saveButton = new Button
        {
            Content = "💾 保存设置",
            Padding = new Thickness(20, 10, 20, 10),
            Margin = new Thickness(0, 0, 10, 0),
            FontSize = 14
        };
        saveButton.Click += async (s, e) =>
        {
            await SaveSettingsAsync();
            _hostService.ShowNotification(
                "设置已保存",
                "示例插件设置已成功保存",
                NotificationType.Success);
        };

        var resetButton = new Button
        {
            Content = "🔄 重置默认",
            Padding = new Thickness(20, 10, 20, 10),
            Margin = new Thickness(0, 0, 10, 0),
            FontSize = 14
        };
        resetButton.Click += async (s, e) =>
        {
            var result = MessageBox.Show(
                "确定要重置所有设置为默认值吗？",
                "确认重置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                await ResetToDefaultAsync();
                _hostService.ShowNotification(
                    "已重置",
                    "设置已恢复为默认值",
                    NotificationType.Information);
            }
        };

        var testButton = new Button
        {
            Content = "🧪 测试设置",
            Padding = new Thickness(20, 10, 20, 10),
            FontSize = 14
        };
        testButton.Click += (s, e) =>
        {
            var greeting = _greetingTextBox?.Text ?? "你好";
            var enabled = _enableFeatureCheckBox?.IsChecked ?? false;
            var theme = _themeComboBox?.SelectedItem?.ToString() ?? "未知";
            var volume = (int)(_volumeSlider?.Value ?? 50);

            MessageBox.Show(
                $"当前设置：\n\n" +
                $"问候语: {greeting}\n" +
                $"高级功能: {(enabled ? "已启用" : "未启用")}\n" +
                $"主题偏好: {theme}\n" +
                $"音量: {volume}%",
                "设置测试",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        };

        panel.Children.Add(saveButton);
        panel.Children.Add(resetButton);
        panel.Children.Add(testButton);

        return panel;
    }

    /// <inheritdoc/>
    public override Task LoadSettingsAsync()
    {
        if (_greetingTextBox != null)
        {
            _greetingTextBox.Text = _configService.GetValue<string>(PluginId, GreetingKey, "你好，世界！");
        }
        
        if (_enableFeatureCheckBox != null)
        {
            _enableFeatureCheckBox.IsChecked = _configService.GetValue<bool>(PluginId, EnableFeatureKey, false);
        }

        if (_themeComboBox != null)
        {
            var themeIndex = _configService.GetValue<int>(PluginId, ThemePreferenceKey, 0);
            _themeComboBox.SelectedIndex = Math.Clamp(themeIndex, 0, 2);
        }

        if (_volumeSlider != null)
        {
            _volumeSlider.Value = _configService.GetValue<double>(PluginId, VolumeKey, 50.0);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task SaveSettingsAsync()
    {
        if (_greetingTextBox != null)
        {
            _configService.SetValue(PluginId, GreetingKey, _greetingTextBox.Text);
        }
        
        if (_enableFeatureCheckBox != null)
        {
            _configService.SetValue(PluginId, EnableFeatureKey, _enableFeatureCheckBox.IsChecked ?? false);
        }

        if (_themeComboBox != null)
        {
            _configService.SetValue(PluginId, ThemePreferenceKey, _themeComboBox.SelectedIndex);
        }

        if (_volumeSlider != null)
        {
            _configService.SetValue(PluginId, VolumeKey, _volumeSlider.Value);
        }

        await _configService.SaveAsync();
    }

    /// <inheritdoc/>
    public override async Task ResetToDefaultAsync()
    {
        if (_greetingTextBox != null)
        {
            _greetingTextBox.Text = "你好，世界！";
        }
        
        if (_enableFeatureCheckBox != null)
        {
            _enableFeatureCheckBox.IsChecked = false;
        }

        if (_themeComboBox != null)
        {
            _themeComboBox.SelectedIndex = 0;
        }

        if (_volumeSlider != null)
        {
            _volumeSlider.Value = 50.0;
        }

        await SaveSettingsAsync();
    }
}
