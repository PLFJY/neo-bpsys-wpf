using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Messages
{
    public class DesignModeChangedMessage(object? sender, bool isDesignMode)
    {
        public object? Sender { get; set; } = sender;
        public bool IsDesignMode { get; set; } = isDesignMode;
    }
}
