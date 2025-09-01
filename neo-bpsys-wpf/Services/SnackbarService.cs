using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
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
        this._presenter = contentPresenter;

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
        if (this._presenter == null)
            throw new InvalidOperationException("The SnackbarPresenter was never set");
        this._snackbar ??= new Snackbar(this._presenter);
        this._snackbar.SetCurrentValue(Snackbar.TitleProperty, (object)title);
        this._snackbar.SetCurrentValue(ContentControl.ContentProperty, (object)content);
        this._snackbar.SetCurrentValue(Snackbar.AppearanceProperty, (object)appearance);
        if(icon != null)
            this._snackbar.SetCurrentValue(Snackbar.IconProperty, (object)icon);
        this._snackbar.SetCurrentValue(Snackbar.TimeoutProperty, (object)(timeout.TotalSeconds == 0.0 ? this.DefaultTimeOut : timeout));
        this._snackbar.SetCurrentValue(Snackbar.IsCloseButtonEnabledProperty, (object)isCloseButtonEnabled);
        this._snackbar.Show(true);
    }
}