using neo_bpsys.Core.Enums;

namespace neo_bpsys.Core.Messages;

public class DesignModeChangedMessage
{
    public DesignModeChangedMessage(object? sender, bool isDesignMode, FrontWindowType? frontWindowType = null)
    {
        Sender = sender;
        IsDesignMode = isDesignMode;
        FrontWindowType = frontWindowType;
    }

    public object? Sender { get; set; }
    public bool IsDesignMode { get; set; }
    public FrontWindowType? FrontWindowType { get; }
}
