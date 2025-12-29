using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Plugins.Services;
using neo_bpsys_wpf.Core.Plugins.UI;

namespace SamplePlugin;

/// <summary>
/// 示例前台窗口扩展
/// </summary>
public class SampleFrontWindowExtension : FrontWindowExtensionBase
{
    private readonly IPluginConfigurationService _configService;
    private readonly IHostApplicationService _hostService;

    public SampleFrontWindowExtension(IPluginConfigurationService configService, IHostApplicationService hostService)
    {
        _configService = configService;
        _hostService = hostService;
    }

    /// <inheritdoc/>
    public override string Id => "sample-front-window";

    /// <inheritdoc/>
    public override string Title => "示例前台窗口";

    /// <inheritdoc/>
    public override double Width => 600;

    /// <inheritdoc/>
    public override double Height => 400;

    /// <inheritdoc/>
    public override bool AllowResize => true;

    /// <inheritdoc/>
    public override bool ShowInTaskbar => true;

    /// <inheritdoc/>
    public override bool Topmost => false;

    /// <inheritdoc/>
    public override FrameworkElement CreateWindowContent()
    {
        var mainGrid = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
        };

        // 创建内容
        var stackPanel = new StackPanel
        {
            Margin = new Thickness(30),
            VerticalAlignment = VerticalAlignment.Top
        };

        // 标题
        var titleBlock = new TextBlock
        {
            Text = "🎨 自定义前台窗口",
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
            Margin = new Thickness(0, 0, 0, 20)
        };

        // 描述
        var descriptionBlock = new TextBlock
        {
            Text = "这是一个由插件创建的自定义前台窗口示例。\n\n" +
                   "前台窗口扩展允许插件：\n" +
                   "• 创建独立的窗口界面\n" +
                   "• 自定义窗口大小和行为\n" +
                   "• 显示复杂的自定义UI\n" +
                   "• 与宿主应用交互",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Margin = new Thickness(0, 0, 0, 30)
        };

        // 示例功能区
        var featurePanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var featureTitle = new TextBlock
        {
            Text = "功能演示：",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        featurePanel.Children.Add(featureTitle);

        // 计数器
        var counterValue = 0;
        var counterText = new TextBlock
        {
            Text = $"计数器: {counterValue}",
            FontSize = 14,
            Margin = new Thickness(0, 5, 0, 10)
        };

        var buttonPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var incrementButton = new Button
        {
            Content = "➕ 增加",
            Padding = new Thickness(15, 8, 15, 8),
            Margin = new Thickness(0, 0, 10, 0),
            MinWidth = 80
        };
        incrementButton.Click += (s, e) =>
        {
            counterValue++;
            counterText.Text = $"计数器: {counterValue}";
        };

        var decrementButton = new Button
        {
            Content = "➖ 减少",
            Padding = new Thickness(15, 8, 15, 8),
            Margin = new Thickness(0, 0, 10, 0),
            MinWidth = 80
        };
        decrementButton.Click += (s, e) =>
        {
            counterValue--;
            counterText.Text = $"计数器: {counterValue}";
        };

        var notifyButton = new Button
        {
            Content = "🔔 发送通知",
            Padding = new Thickness(15, 8, 15, 8),
            MinWidth = 100
        };
        notifyButton.Click += (s, e) =>
        {
            _hostService.ShowNotification(
                "来自插件窗口",
                $"当前计数器值: {counterValue}",
                NotificationType.Information);
        };

        buttonPanel.Children.Add(incrementButton);
        buttonPanel.Children.Add(decrementButton);
        buttonPanel.Children.Add(notifyButton);

        featurePanel.Children.Add(counterText);
        featurePanel.Children.Add(buttonPanel);

        // 配置区域
        var configPanel = new StackPanel
        {
            Margin = new Thickness(0, 20, 0, 0)
        };

        var configTitle = new TextBlock
        {
            Text = "配置信息：",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var greetingText = _configService.GetValue<string>("com.sample.plugin", "greeting", "你好，世界！");
        var configText = new TextBlock
        {
            Text = $"当前问候语: {greetingText}",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
        };

        configPanel.Children.Add(configTitle);
        configPanel.Children.Add(configText);

        // 组装所有内容
        stackPanel.Children.Add(titleBlock);
        stackPanel.Children.Add(descriptionBlock);
        stackPanel.Children.Add(featurePanel);
        stackPanel.Children.Add(configPanel);

        mainGrid.Children.Add(stackPanel);

        return mainGrid;
    }
}
