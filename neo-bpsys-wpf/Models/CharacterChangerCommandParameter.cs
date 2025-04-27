using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Models
{
    public class CharacterChangerCommandParameter
    {
        public int Index { get; set; }
        public int ButtonContent { get; set; }

        public CharacterChangerCommandParameter(int index, int buttonContent)
        {
            Index = index;
            ButtonContent = buttonContent;
        }
    }
}
