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
        public static ImageBrush? GetUiImageBrush(string key)
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
        /// Get Character ImageSource from corresponding Resources folder
        /// </summary>
        /// <param name="key">ImageSourceKey</param>
        /// <param name="characterFileName">characterFileName</param>
        /// <returns></returns>
        public static ImageSource? GetCharacterImageSource(ImageSourceKey key, string? characterFileName)
        {
            if (string.IsNullOrEmpty(characterFileName)) return null;

            return new BitmapImage(
                new Uri($"{Environment.CurrentDirectory}\\Resources\\{key}\\{characterFileName}")
            );
        }

        /// <summary>
        /// Get Map ImageSource from corresponding Resources folder <br/> ImageSourceKey usually like <see cref="ImageSourceKey.map"/> or <see cref="ImageSourceKey.map_singleColor"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static ImageSource? GetMapImageSource(ImageSourceKey key, string? map)
        {
            if (string.IsNullOrEmpty(map)) return null;

            return new BitmapImage(
                new Uri($"{Environment.CurrentDirectory}\\Resources\\{key}\\{map}.png")
            );
        }
    }
}
