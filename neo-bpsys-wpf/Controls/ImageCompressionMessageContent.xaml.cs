using System.Windows.Controls;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// 显示图片压缩建议的消息内容控件。
/// </summary>
public partial class ImageCompressionMessageContent : UserControl
{
    /// <summary>
    /// 获取要显示的图片压缩建议。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 初始化 <see cref="ImageCompressionMessageContent"/> 的新实例。
    /// </summary>
    /// <param name="message">要显示的图片压缩建议。</param>
    public ImageCompressionMessageContent(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Message = message;
        InitializeComponent();
        DataContext = this;
    }
}
