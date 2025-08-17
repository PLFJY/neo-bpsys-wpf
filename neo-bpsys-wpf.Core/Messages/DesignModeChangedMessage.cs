using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Messages;

/// <summary>
/// 設計モード変更メッセージ
/// </summary>
/// <param name="sender">送信元</param>
/// <param name="isDesignMode">設計モード</param>
/// <param name="frontWindowType">フロントウィンドウ種別</param>
public class DesignModeChangedMessage(object? sender, bool isDesignMode, FrontWindowType? frontWindowType = null)
{
    /// <summary>
    /// 送信元
    /// </summary>
    public object? Sender { get; set; } = sender;
    /// <summary>
    /// 設計モード
    /// </summary>
    public bool IsDesignMode { get; set; } = isDesignMode;
    /// <summary>
    /// フロットウィンドウ種別
    /// </summary>
    public FrontWindowType? FrontWindowType { get; } = frontWindowType;
}