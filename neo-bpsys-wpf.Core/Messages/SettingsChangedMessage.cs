using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Messages;

public class SettingsChangedMessage(FrontWindowType windowType)
{
    public FrontWindowType WindowType { get; } = windowType;
}