using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Plugins.UI;

namespace SamplePlugin;

/// <summary>
/// 示例导航页面扩展
/// </summary>
public class SampleNavigationPageExtension : NavigationPageExtensionBase
{
    /// <inheritdoc/>
    public override string Id => "sample-plugin-page";

    /// <inheritdoc/>
    public override string Title => "示例插件页面";

    /// <inheritdoc/>
    public override string? Description => "这是由示例插件提供的页面";

    /// <inheritdoc/>
    public override Type PageType => typeof(SamplePage);

    /// <inheritdoc/>
    public override int Priority => 1000; // 放在较后的位置
}

/// <summary>
/// 示例页面 - 展示插件的各种功能
/// </summary>
public class SamplePage : UserControl
{
    public SamplePage()
    {
        Content = CreateContent();
    }

    private FrameworkElement CreateContent()
    {
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(20)
        };

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(20)
        };

        // === 标题区域 ===
        var titleBlock = new TextBlock
        {
            Text = "🎉 示例插件完整功能演示",
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            Margin = new Thickness(0, 0, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var subtitleBlock = new TextBlock
        {
            Text = "这个页面展示了插件系统的所有可用功能",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
            Margin = new Thickness(0, 0, 0, 30),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        mainPanel.Children.Add(titleBlock);
        mainPanel.Children.Add(subtitleBlock);

        // === 功能列表 ===
        mainPanel.Children.Add(CreateFeatureSection());

        // === 交互示例 ===
        mainPanel.Children.Add(CreateInteractiveSection());

        // === 信息展示 ===
        mainPanel.Children.Add(CreateInfoSection());

        scrollViewer.Content = mainPanel;
        return scrollViewer;
    }

    private FrameworkElement CreateFeatureSection()
    {
        var section = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 100, 150, 250)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 20)
        };

        var panel = new StackPanel();

        var sectionTitle = new TextBlock
        {
            Text = "✨ 插件系统功能",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 15)
        };
        panel.Children.Add(sectionTitle);

        var features = new[]
        {
            "📄 导航页面扩展 - 向应用添加自定义页面",
            "⚙️ 设置页面扩展 - 提供插件配置界面",
            "🪟 前台窗口扩展 - 创建独立的自定义窗口",
            "📡 事件订阅 - 监听应用程序事件",
            "💾 配置持久化 - 保存和加载插件配置",
            "🔧 服务注入 - 注册和使用自定义服务",
            "📢 通知系统 - 向用户显示通知消息",
            "🎨 主题感知 - 响应应用主题变更"
        };

        foreach (var feature in features)
        {
            var featureText = new TextBlock
            {
                Text = feature,
                FontSize = 14,
                Margin = new Thickness(10, 5, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(featureText);
        }

        section.Child = panel;
        return section;
    }

    private FrameworkElement CreateInteractiveSection()
    {
        var section = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 100, 200, 100)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 20)
        };

        var panel = new StackPanel();

        var sectionTitle = new TextBlock
        {
            Text = "🎮 交互功能演示",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 15)
        };
        panel.Children.Add(sectionTitle);

        // 计数器
        var counter = 0;
        var counterDisplay = new TextBlock
        {
            Text = $"点击次数: {counter}",
            FontSize = 16,
            Margin = new Thickness(0, 10, 0, 10)
        };

        var clickButton = new Button
        {
            Content = "🖱️ 点击我",
            Padding = new Thickness(20, 10, 20, 10),
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 20)
        };
        clickButton.Click += (s, e) =>
        {
            counter++;
            counterDisplay.Text = $"点击次数: {counter}";
            
            if (counter % 5 == 0)
            {
                MessageBox.Show(
                    $"你已经点击了 {counter} 次！继续加油！",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        };

        panel.Children.Add(counterDisplay);
        panel.Children.Add(clickButton);

        // 输入测试
        var inputLabel = new TextBlock
        {
            Text = "输入测试:",
            FontSize = 14,
            Margin = new Thickness(0, 10, 0, 5)
        };

        var inputBox = new TextBox
        {
            Padding = new Thickness(8),
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var inputButton = new Button
        {
            Content = "显示输入内容",
            Padding = new Thickness(15, 8, 15, 8),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        inputButton.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(inputBox.Text))
            {
                MessageBox.Show(
                    $"你输入的内容是：\n{inputBox.Text}",
                    "输入内容",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        };

        panel.Children.Add(inputLabel);
        panel.Children.Add(inputBox);
        panel.Children.Add(inputButton);

        section.Child = panel;
        return section;
    }

    private FrameworkElement CreateInfoSection()
    {
        var section = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 200, 100, 100)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 20)
        };

        var panel = new StackPanel();

        var sectionTitle = new TextBlock
        {
            Text = "ℹ️ 插件信息",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 15)
        };
        panel.Children.Add(sectionTitle);

        var infoItems = new[]
        {
            ("插件ID", "com.sample.plugin"),
            ("插件名称", "示例插件"),
            ("版本", "1.0.0"),
            ("作者", "Plugin Developer"),
            ("加载状态", "✅ 已加载"),
            ("运行环境", ".NET 9.0 / WPF")
        };

        foreach (var (label, value) in infoItems)
        {
            var infoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 5)
            };

            var labelText = new TextBlock
            {
                Text = $"{label}:",
                FontWeight = FontWeights.SemiBold,
                Width = 120,
                FontSize = 14
            };

            var valueText = new TextBlock
            {
                Text = value,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80))
            };

            infoPanel.Children.Add(labelText);
            infoPanel.Children.Add(valueText);
            panel.Children.Add(infoPanel);
        }

        section.Child = panel;
        return section;
    }
}
