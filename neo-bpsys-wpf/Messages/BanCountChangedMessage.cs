using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Messages
{
    public class BanCountChangedMessage(string changedList)
    {
        public string ChangedList { get; set; } = changedList;
    }
}
