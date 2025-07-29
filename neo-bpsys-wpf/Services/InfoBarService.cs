using neo_bpsys_wpf.Core.Abstractions.Services;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 提示框服务, 实现了 <see cref="IInfoBarService"/> 接口，负责显示提示框
/// </summary>
public class InfoBarService : IInfoBarService
{
    private InfoBar? _infoBar;
    /// <summary>
    /// 设置提示框控件
    /// </summary>
    /// <param name="infoBar"></param>
    public void SetInfoBarControl(InfoBar infoBar)
    {
        _infoBar = infoBar;
    }
    /// <summary>
    /// 显示错误提示框
    /// </summary>
    /// <param name="message"></param>
    public void ShowErrorInfoBar(string message)
    {
        if (_infoBar == null) return;
        _infoBar.Message = message;
        _infoBar.Severity = InfoBarSeverity.Error;
        _infoBar.IsOpen = true;
    }
    /// <summary>
    /// 显示信息提示框
    /// </summary>
    /// <param name="message"></param>
    public void ShowInformationalInfoBar(string message)
    {
        if (_infoBar == null) return;
        _infoBar.Message = message;
        _infoBar.Severity = InfoBarSeverity.Informational;
        _infoBar.IsOpen = true;
    }
    /// <summary>
    /// 显示成功提示框
    /// </summary>
    /// <param name="message"></param>
    public void ShowSuccessInfoBar(string message)
    {
        if (_infoBar == null) return;
        _infoBar.Message = message;
        _infoBar.Severity = InfoBarSeverity.Success;
        _infoBar.IsOpen = true;
    }
    /// <summary>
    /// 显示警告提示框
    /// </summary>
    /// <param name="message"></param>
    public void ShowWarningInfoBar(string message)
    {
        if (_infoBar == null) return;
        _infoBar.Message = message;
        _infoBar.Severity = InfoBarSeverity.Warning;
        _infoBar.IsOpen = true;
    }
    /// <summary>
    /// 关闭提示框
    /// </summary>
    public void CloseInfoBar()
    {
        if (_infoBar == null) return;
        _infoBar.IsOpen = false;
    }
}