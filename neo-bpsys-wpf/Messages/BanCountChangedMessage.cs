using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Messages
{
    public class BanCountChangedMessage(BanListName changedList)
    {
        public BanListName ChangedList { get; set; } = changedList;
    }
}
