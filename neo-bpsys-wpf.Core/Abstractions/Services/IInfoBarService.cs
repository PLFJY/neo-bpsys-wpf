using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 信息框服务接口
/// </summary>
public interface IInfoBarService
{
    /// <summary>
    /// 关闭 InfoBar
    /// </summary>
    void CloseInfoBar();
    /// <summary>
    /// 设置 InfoBar 控件
    /// </summary>
    /// <param name="infoBar"></param>
    void SetInfoBarControl(InfoBar infoBar);
    /// <summary>
    /// 显示错误信息
    /// </summary>
    /// <param name="message"></param>
    void ShowErrorInfoBar(string message);
    /// <summary>
    /// 显示提示信息
    /// </summary>
    /// <param name="message"></param>
    void ShowInformationalInfoBar(string message);
    /// <summary>
    /// 显示成功信息
    /// </summary>
    /// <param name="message"></param>
    void ShowSuccessInfoBar(string message);
    /// <summary>
    /// 显示警告信息
    /// </summary>
    /// <param name="message"></param>
    void ShowWarningInfoBar(string message);
}