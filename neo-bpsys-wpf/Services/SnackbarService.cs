using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Services;

public class SnackbarService : ISnackbarService
{
    private SnackbarPresenter? _presenter;
    private Snackbar? _snackbar;

    /// <summary>
    /// 默认关闭时间
    /// </summary>
    public TimeSpan DefaultTimeOut { get; set; } = TimeSpan.FromSeconds(5L);

    /// <summary>
    /// 设置<see cref="SnackbarPresenter"/>控件
    /// </summary>
    /// <param name="contentPresenter"><see cref="SnackbarPresenter"/>控件</param>
    public void SetSnackbarPresenter(SnackbarPresenter contentPresenter)
    {
        _presenter = contentPresenter;

    }

    /// <summary>
    /// 显示
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="content">显示内容</param>
    /// <param name="appearance"><see cref="SnackbarPresenter"/>类型</param>
    /// <param name="icon">图标</param>
    /// <param name="timeout">超时自动隐藏时间</param>
    /// <param name="isCloseButtonEnabled">关闭按钮是否可用</param>
    /// <exception cref="InvalidOperationException">未设置SnackbarControl</exception>
    public void Show(
        string title,
        DependencyObject content,
        ControlAppearance appearance,
        IconElement? icon,
        TimeSpan timeout,
        bool isCloseButtonEnabled)
    {
        if (_presenter == null)
            throw new InvalidOperationException("The SnackbarPresenter was never set");
        _snackbar ??= new Snackbar(_presenter);
        _snackbar.SetCurrentValue(Snackbar.TitleProperty, (object)title);
        _snackbar.SetCurrentValue(ContentControl.ContentProperty, (object)content);
        _snackbar.SetCurrentValue(Snackbar.AppearanceProperty, (object)appearance);
        if (icon != null)
            _snackbar.SetCurrentValue(Snackbar.IconProperty, (object)icon);
        _snackbar.SetCurrentValue(Snackbar.TimeoutProperty, (object)(timeout.TotalSeconds == 0.0 ? DefaultTimeOut : timeout));
        _snackbar.SetCurrentValue(Snackbar.IsCloseButtonEnabledProperty, (object)isCloseButtonEnabled);
        _snackbar.Show(true);
    }

    /// <summary>
    /// 隐藏 <see cref="SnackbarPresenter"/>
    /// </summary>
    public void Hide()
    {
        if (_snackbar == null) return;
        _presenter?.HideCurrent();
    }
}