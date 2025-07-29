using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Messages;

public class BanCountChangedMessage(BanListName changedList)
{
    public BanListName ChangedList { get; set; } = changedList;
}