using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 消息框助手
/// </summary>
public static class MessageBoxHelper
{
    /// <summary>
    /// 显示信息对话框
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="title">标题 (默认值 "Notification" 未国际化，如需国际化文本请手动传入)</param>
    /// <param name="closeButtonText">关闭按钮文本 (默认值 "Cancel" 未国际化，如需国际化文本请手动传入)</param>
    /// <returns></returns>
    public static async Task ShowInfoAsync(string message, string title = "Notification", string closeButtonText = "Cancel")
    {
        var messageBox = new MessageBox()
        {
            Title = title,
            Content = message,
            CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Dismiss24 },
            CloseButtonText = closeButtonText
        };

        await messageBox.ShowDialogAsync();
    }

    /// <summary>
    /// 显示错误对话框
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="title">标题 (默认值 "Error" 未国际化，如需国际化文本请手动传入)</param>
    /// <param name="closeButtonText">关闭按钮文本 (默认值 "Close" 未国际化，如需国际化文本请手动传入)</param>
    /// <returns></returns>
    public static async Task ShowErrorAsync(string message, string title = "Error", string closeButtonText = "Close")
    {
        var messageBox = new MessageBox()
        {
            Title = title,
            Content = message,
            CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Dismiss24 },
            CloseButtonText = closeButtonText
        };

        await messageBox.ShowDialogAsync();
    }

    /// <summary>
    /// 显示确认对话框
    /// </summary>
    /// <param name="message">标题</param>
    /// <param name="title">消息内容</param>
    /// <param name="primaryButtonText">主按钮文本 (默认值 "Confirm" 未国际化，如需国际化文本请手动传入)</param>
    /// <param name="secondaryButtonText">次按钮文本 (默认值 "Cancel" 未国际化，如需国际化文本请手动传入)</param>
    /// <returns>用户是否确认</returns>
    public static async Task<bool> ShowConfirmAsync(string message, string title, string primaryButtonText = "Confirm",
        string secondaryButtonText = "Cancel")
    {
        var messageBox = new MessageBox()
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = secondaryButtonText,
            CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Dismiss24 },
            PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Checkmark24 }
        };
        var result = await messageBox.ShowDialogAsync();

        return result == MessageBoxResult.Primary;
    }
}