using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Helpers
{
    public static class ImageHelper
    {
        /// <summary>
        /// Get Ui ImageBrush from Resources\bpui\
        /// </summary>
        /// <param name="key">ui image filename without filename extension</param>
        /// <returns></returns>
        public static ImageBrush GetUiImage(string key)
        {
            return new ImageBrush(new BitmapImage(new Uri($"{Environment.CurrentDirectory}\\Resources\\bpui\\{key}.png")));
        }
    }
}
