namespace neo_bpsys_wpf.Core.Messages;

/// <summary>
/// 前台布局包变更消息
/// </summary>
/// <param name="Sender">消息发送者</param>
/// <param name="ActivePackageId">当前激活的布局包ID</param>
public record FrontedLayoutPackagesChangedMessage(object? Sender, string? ActivePackageId);
