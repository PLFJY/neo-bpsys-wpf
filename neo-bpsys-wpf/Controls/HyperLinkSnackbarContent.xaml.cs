using CommunityToolkit.Mvvm.Input;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// 带超链接的快照栏内容控件，提供文本和"不再显示"操作。
/// </summary>
public partial class HyperLinkSnackbarContent : UserControl
{
    /// <summary>
    /// 获取快照栏显示的文本内容。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 获取"不再显示"按钮的文本。
    /// </summary>
    public string NoLongerDisplayText { get; }

    /// <summary>
    /// 获取"不再显示"按钮的命令。
    /// </summary>
    public ICommand NoLogerDisplayedCommand { get; }

    /// <summary>
    /// 初始化 <see cref="HyperLinkSnackbarContent"/> 的新实例。
    /// </summary>
    /// <param name="text">快照栏显示的文本内容。</param>
    /// <param name="noLongerDisplayText">"不再显示"按钮的文本。</param>
    /// <param name="noLogerDisplayedAction">点击"不再显示"按钮时执行的操作。</param>
    public HyperLinkSnackbarContent(string text, string noLongerDisplayText, Action noLogerDisplayedAction)
    {
        InitializeComponent();
        Text = text;
        NoLongerDisplayText = noLongerDisplayText;
        NoLogerDisplayedCommand = new RelayCommand(noLogerDisplayedAction, () => true);
        DataContext = this;
    }
}