using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Messages
{
    public class MemberStateChangedMessage(object? sender)
    {
        public object? Sender { get; set; } = sender;
    }
}
