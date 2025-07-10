using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Messages;

public class SettingsChangedMessage(FrontWindowType windowType)
{
    public FrontWindowType WindowType { get; } = windowType;
}