using System.Windows.Media;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Enums;

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
            return new ImageBrush(
                new BitmapImage(
                    new Uri(
                        $"{Environment.CurrentDirectory}\\Resources\\{ImageSourceKey.bpui}\\{key}.png"
                    )
                )
            );
        }

        /// <summary>
        /// Get Character ImageBrush from correspinding Resources folder
        /// </summary>
        /// <param name="key">ImageSourceKey</param>
        /// <param name="character">character name</param>
        /// <returns></returns>
        public static ImageSource GetCharacterImageBrush(ImageSourceKey key, string character)
        {
            return new BitmapImage(
                new Uri($"{Environment.CurrentDirectory}\\Resources\\{key}\\{character}.png")
            );
        }

        /// <summary>
        /// Get ImageSource from file path
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static ImageSource GetImageFromPath(string fileName)
        {
            return new BitmapImage(new Uri(fileName));
        }
    }
}
