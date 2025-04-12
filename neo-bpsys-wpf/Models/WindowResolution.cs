using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Models
{
    public class WindowResolution(int width, int height)
    {
        public int Width { get; set; } = width;

        public int Height { get; set; } = height;
    }
}
