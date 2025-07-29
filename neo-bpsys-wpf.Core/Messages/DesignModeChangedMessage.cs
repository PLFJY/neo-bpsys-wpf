using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Messages;

public class DesignModeChangedMessage(object? sender, bool isDesignMode, FrontWindowType? frontWindowType = null)
{
    public object? Sender { get; set; } = sender;
    public bool IsDesignMode { get; set; } = isDesignMode;
    public FrontWindowType? FrontWindowType { get; } = frontWindowType;
}