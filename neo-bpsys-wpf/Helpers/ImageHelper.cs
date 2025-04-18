using neo_bpsys_wpf.Enums;
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
        public static ImageBrush GetUiImageBrush(string key)
        {
            return new ImageBrush(new BitmapImage(new Uri($"{Environment.CurrentDirectory}\\Resources\\{ImageSourceKey.bpui}\\{key}.png")));
        }
        /// <summary>
        /// Get Character ImageBrush from correspinding Resources folder
        /// </summary>
        /// <param name="key">ImageSourceKey</param>
        /// <param name="character">character name</param>
        /// <returns></returns>
        public static BitmapImage GetCharacterImageBrush(ImageSourceKey key, string character)
        {
            return new BitmapImage(new Uri($"{Environment.CurrentDirectory}\\Resources\\{key}\\{character}.png"));
        }
    }
}
