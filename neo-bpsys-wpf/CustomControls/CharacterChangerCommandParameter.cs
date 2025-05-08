using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.CustomControls
{
    public class CharacterChangerCommandParameter
    {
        public int Target { get; set; }
        public int Source { get; set; }

        public CharacterChangerCommandParameter(int index, int buttonContent)
        {
            Target = index;
            Source = buttonContent;
        }
    }
}
