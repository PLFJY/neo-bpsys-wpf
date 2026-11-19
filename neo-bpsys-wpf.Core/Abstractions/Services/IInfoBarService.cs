namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 信息框服务接口
/// </summary>
public interface IInfoBarService
{
    /// <summary>
    /// 请求关闭当前 InfoBar；可见展示不足五秒时延后关闭，用户手动关闭不受限制。
    /// </summary>
    void CloseInfoBar();
    /// <summary>
    /// 设置 InfoBar 控件，并呈现控件就绪前收到的最新消息。
    /// </summary>
    /// <param name="infoBar">InfoBar 控件实例</param>
    void SetInfoBarControl(object infoBar);
    /// <summary>
    /// 显示错误信息
    /// </summary>
    /// <param name="message">错误消息文本</param>
    void ShowErrorInfoBar(string message);
    /// <summary>
    /// 显示提示信息
    /// </summary>
    /// <param name="message">提示消息文本</param>
    void ShowInformationalInfoBar(string message);
    /// <summary>
    /// 显示成功信息
    /// </summary>
    /// <param name="message">成功消息文本</param>
    void ShowSuccessInfoBar(string message);
    /// <summary>
    /// 显示警告信息
    /// </summary>
    /// <param name="message">警告消息文本</param>
    void ShowWarningInfoBar(string message);
}
