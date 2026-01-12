using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Messages;

/// <summary>
/// 設計モード変更メッセージ
/// </summary>
/// <param name="Sender">送信元</param>
/// <param name="IsDesignerMode">設計モード</param>
/// <param name="FrontedWindowId">ウィンドウID</param>
public record DesignerModeChangedMessage(object? Sender, bool IsDesignerMode, string? FrontedWindowId = null);