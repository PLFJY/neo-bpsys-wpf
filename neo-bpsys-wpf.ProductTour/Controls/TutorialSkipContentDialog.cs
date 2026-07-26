using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ProductTour.Controls;

/// <summary>用户在教程跳过对话框中做出的选择。</summary>
public enum TutorialSkipChoice
{
    /// <summary>取消跳过并继续教程。</summary>
    Continue,
    /// <summary>仅跳过当前播放。</summary>
    SkipForCurrentSession,
    /// <summary>永久跳过。</summary>
    SkipPermanently
}

/// <summary>使用 WPF-UI 内容对话框确认教程跳过操作。</summary>
public static class TutorialSkipContentDialog
{
    /// <summary>显示跳过确认对话框。</summary>
    /// <param name="host">承载对话框的主机。</param>
    /// <param name="textProvider">本地化文本提供程序。</param>
    /// <param name="description">当前导览类型的说明。</param>
    /// <param name="sessionSuppression">当前进程的新手教程显示抑制器。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>用户选择的跳过范围。</returns>
    public static async Task<TutorialSkipChoice> ShowAsync(ContentDialogHost host, ITutorialTextProvider textProvider, string description, ITutorialSessionSuppression sessionSuppression, CancellationToken cancellationToken = default)
    {
        var suppressCheckBox = new CheckBox { Content = textProvider.SuppressUntilNextStartup, Margin = new Thickness(0, 16, 0, 0) };
        var dialog = new ContentDialog(host)
        {
            Title = textProvider.SkipConfirmTitle,
            Content = new StackPanel { Children = { new System.Windows.Controls.TextBlock { Text = description, TextWrapping = TextWrapping.Wrap }, suppressCheckBox } },
            PrimaryButtonText = textProvider.SkipForCurrentSession,
            SecondaryButtonText = textProvider.SkipPermanently,
            CloseButtonText = textProvider.SkipConfirmContinue,
            DefaultButton = ContentDialogButton.Close
        };
        var result = await dialog.ShowAsync(cancellationToken);
        if (suppressCheckBox.IsChecked == true)
        {
            sessionSuppression.SuppressUntilNextStartup();
        }

        return result switch
        {
            ContentDialogResult.Primary => TutorialSkipChoice.SkipForCurrentSession,
            ContentDialogResult.Secondary => TutorialSkipChoice.SkipPermanently,
            _ => TutorialSkipChoice.Continue
        };
    }
}
