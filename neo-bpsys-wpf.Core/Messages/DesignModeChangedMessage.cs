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
    public object? Sender { get; set; } = sender;
    public bool IsDesignMode { get; set; } = isDesignMode;
    public FrontWindowType? FrontWindowType { get; } = frontWindowType;
}