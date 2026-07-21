using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// SmartBP 模块内容视图。
/// </summary>
public partial class SmartBpModuleContentView : UserControl
{
    private readonly ITutorialRunner? _tutorialRunner;
    private CancellationTokenSource _tutorialLifetime = new();
    private Task<TutorialRunResult>? _tutorialRun;

    /// <summary>
    /// 初始化 <see cref="SmartBpModuleContentView"/> 类的新实例。
    /// </summary>
    /// <param name="tutorialRunner">教程运行器。</param>
    public SmartBpModuleContentView(ITutorialRunner? tutorialRunner = null)
    {
        InitializeComponent();
        _tutorialRunner = tutorialRunner;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_tutorialLifetime.IsCancellationRequested)
        {
            _tutorialLifetime.Dispose();
            _tutorialLifetime = new CancellationTokenSource();
        }

        // Capture the token into a local before awaiting. _tutorialLifetime may be
        // replaced (see above) between the InvokeAsync call and the catch filter;
        // checking the field in the filter would then read a fresh, uncanceled CTS
        // and let the OperationCanceledException escape this async void method,
        // crashing the Dispatcher. The local snapshot always matches the token that
        // was actually handed to the awaited operations.
        var token = _tutorialLifetime.Token;
        try
        {
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle, token);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render, token);
            var runner = _tutorialRunner;
            if (runner == null)
            {
                return;
            }

            if (_tutorialRun is not { IsCompleted: false })
            {
                _tutorialRun = runner.RunSequenceAsync(this, TutorialPageKey, token);
            }

            await _tutorialRun;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _tutorialLifetime.Cancel();
    }

    /// <summary>
    /// AI 调试控制台文本变化时自动滚动到末尾。
    /// </summary>
    /// <param name="sender">触发事件的文本框。</param>
    /// <param name="e">文本变化事件参数。</param>
    private void AiDebugConsoleTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.ScrollToEnd();
    }

    /// <summary>
    /// 在调试文本框内部消费可滚动方向的鼠标滚轮事件，避免外层滚动容器抢先滚动。
    /// </summary>
    /// <param name="sender">触发滚轮事件的文本框。</param>
    /// <param name="e">鼠标滚轮事件参数。</param>
    private void DebugTextBox_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var scrollViewer = FindDescendant<ScrollViewer>(textBox);
        if (scrollViewer is null || scrollViewer.ScrollableHeight <= 0)
            return;

        var isWheelUp = e.Delta > 0;
        var canScrollInside = isWheelUp
            ? scrollViewer.VerticalOffset > 0
            : scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;
        if (!canScrollInside)
            return;

        var wheelLines = Math.Max(1, SystemParameters.WheelScrollLines);
        var targetOffset = scrollViewer.VerticalOffset - e.Delta / 120D * wheelLines * 16D;
        scrollViewer.ScrollToVerticalOffset(Math.Clamp(targetOffset, 0D, scrollViewer.ScrollableHeight));
        e.Handled = true;
    }

    /// <summary>
    /// 从指定可视树节点下查找第一个指定类型的后代元素。
    /// </summary>
    /// <typeparam name="T">要查找的后代元素类型。</typeparam>
    /// <param name="parent">查找起点。</param>
    /// <returns>找到的第一个后代元素；未找到时返回 <see langword="null"/>。</returns>
    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
                return typedChild;
            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }
}
