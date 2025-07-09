using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Messages
{
    public class BanCountChangedMessage(BanListName changedList)
    {
        public BanListName ChangedList { get; set; } = changedList;
    }
}
