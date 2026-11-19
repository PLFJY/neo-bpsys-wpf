using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using neo_bpsys_wpf.Core.Abstractions.Services;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Services;

/// <summary>在宿主 UI 线程串行呈现提示，每条消息至少可见五秒后才允许程序关闭或替换。</summary>
public class InfoBarService : IInfoBarService
{
    private static readonly TimeSpan MinimumVisibleTime = TimeSpan.FromSeconds(5);
    private static readonly DependencyPropertyDescriptor OpenDescriptor =
        DependencyPropertyDescriptor.FromProperty(InfoBar.IsOpenProperty, typeof(InfoBar));
    private readonly ConcurrentQueue<Notification> _pending = new();
    private readonly Stopwatch _visibleTime = new();
    private InfoBar? _infoBar;
    private DispatcherTimer? _timer;
    private Notification? _current;
    private bool _closeRequested;
    private bool _updating;

    /// <inheritdoc/>
    public void SetInfoBarControl(object infoBar)
    {
        if (infoBar is not InfoBar control) return;
        if (!control.Dispatcher.CheckAccess())
        {
            control.Dispatcher.Invoke(() => SetInfoBarControl(control));
            return;
        }
        if (ReferenceEquals(_infoBar, control)) return;
        if (_infoBar is not null)
        {
            _infoBar.IsVisibleChanged -= OnVisibilityChanged;
            OpenDescriptor.RemoveValueChanged(_infoBar, OnOpenChanged);
        }
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        }
        _infoBar = control;
        _timer = new DispatcherTimer(DispatcherPriority.Background, control.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += OnTick;
        control.IsVisibleChanged += OnVisibilityChanged;
        OpenDescriptor.AddValueChanged(control, OnOpenChanged);
        if (_current is not null) Present(_current);
        else Pump();
    }

    /// <inheritdoc/>
    public void ShowErrorInfoBar(string message) => Enqueue(message, InfoBarSeverity.Error);

    /// <inheritdoc/>
    public void ShowInformationalInfoBar(string message) => Enqueue(message, InfoBarSeverity.Informational);

    /// <inheritdoc/>
    public void ShowSuccessInfoBar(string message) => Enqueue(message, InfoBarSeverity.Success);

    /// <inheritdoc/>
    public void ShowWarningInfoBar(string message) => Enqueue(message, InfoBarSeverity.Warning);

    /// <inheritdoc/>
    public void CloseInfoBar() => Dispatch(() =>
    {
        if (_current is null) return;
        _closeRequested = true;
        Pump();
    });

    private void Enqueue(string message, InfoBarSeverity severity)
    {
        _pending.Enqueue(new Notification(message, severity));
        Dispatch(Pump);
    }

    private void Dispatch(Action action)
    {
        var dispatcher = _infoBar?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    private void Pump()
    {
        if (_infoBar is null) return;
        if (_current is not null && _visibleTime.Elapsed < MinimumVisibleTime) return;
        if (_pending.TryDequeue(out var next))
        {
            Present(next);
        }
        else if (_closeRequested)
        {
            _updating = true;
            try { _infoBar.IsOpen = false; }
            finally { _updating = false; }
            ClearCurrent();
        }
        else if (_current is not null)
        {
            // 已满足保底时间，保持原有的常驻语义，不持续唤醒 UI 线程。
            _timer?.Stop();
        }
    }

    private void Present(Notification notification)
    {
        _current = notification;
        _closeRequested = false;
        _visibleTime.Reset();
        _updating = true;
        try
        {
            _infoBar!.Message = notification.Message;
            _infoBar.Severity = notification.Severity;
            _infoBar.IsOpen = true;
        }
        finally { _updating = false; }
        UpdateVisibility();
    }

    private void ClearCurrent()
    {
        _current = null;
        _closeRequested = false;
        _visibleTime.Reset();
        _timer?.Stop();
    }

    private void UpdateVisibility()
    {
        if (_current is not null && _infoBar is { IsOpen: true, IsVisible: true })
        {
            _visibleTime.Start();
            _timer?.Start();
        }
        else
        {
            _visibleTime.Stop();
            _timer?.Stop();
        }
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateVisibility();

    private void OnOpenChanged(object? sender, EventArgs e)
    {
        if (_updating || _infoBar?.IsOpen != false) return;
        // 用户点关闭按钮时立即尊重其选择，下一条仍重新计算五秒。
        ClearCurrent();
        Pump();
    }

    private void OnTick(object? sender, EventArgs e) => Pump();

    private sealed record Notification(string Message, InfoBarSeverity Severity);
}
