using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface ISnackbarService
{
    /// <summary>
    /// 默认关闭时间
    /// </summary>
    TimeSpan DefaultTimeOut { get; set; }

    /// <summary>
    /// 隐藏 <see cref="SnackbarPresenter"/>
    /// </summary>
    void Hide();

    /// <summary>
    /// 设置<see cref="SnackbarPresenter"/>控件
    /// </summary>
    /// <param name="contentPresenter"><see cref="SnackbarPresenter"/>控件</param>
    void SetSnackbarPresenter(SnackbarPresenter contentPresenter);

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
    void Show(
        string title,
        DependencyObject content,
        ControlAppearance appearance,
        IconElement? icon,
        TimeSpan timeout,
        bool isCloseButtonEnabled
    );
}