using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Messages;

public class DesignModeChangedMessage(object? sender, bool isDesignMode, FrontWindowType? frontWindowType = null)
{
    public object? Sender { get; set; } = sender;
    public bool IsDesignMode { get; set; } = isDesignMode;
    public FrontWindowType? FrontWindowType { get; } = frontWindowType;
}