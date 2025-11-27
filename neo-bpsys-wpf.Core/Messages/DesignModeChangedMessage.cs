using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Messages;

/// <summary>
/// 設計モード変更メッセージ
/// </summary>
/// <param name="Sender">送信元</param>
/// <param name="IsDesignMode">設計モード</param>
/// <param name="FrontWindowType">フロントウィンドウ種別</param>
public record DesignModeChangedMessage(object? Sender, bool IsDesignMode, FrontWindowType? FrontWindowType = null);