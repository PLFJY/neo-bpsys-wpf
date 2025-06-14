using neo_bpsys_wpf.Enums;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Helpers
{
    public static class ImageHelper
    {
        /// <summary>
        /// Get Ui ImageBrush from Resources\bpui\
        /// </summary>
        /// <param name="key">ui _image filename without filename extension</param>
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
        /// Get Ui ImageSource from Resources\bpui\
        /// </summary>
        /// <param name="key">ui _image filename without filename extension</param>
        /// <returns></returns>
        public static ImageSource? GetUiImageSource(string key)
        {
            return new BitmapImage(
                    new Uri(
                        $"{Environment.CurrentDirectory}\\Resources\\{ImageSourceKey.bpui}\\{key}.png"
                    )
            );
        }

        /// <summary>
        /// Get ImageSource from corresponding Resources folder
        /// </summary>
        /// <param name="key">ImageSourceKey</param>
        /// <param name="fileName">file name</param>
        /// <returns></returns>
        public static ImageSource? GetImageSourceFromFileName(ImageSourceKey key, string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            if (!File.Exists($"{Environment.CurrentDirectory}\\Resources\\{key}\\{fileName}")) return null;

            return new BitmapImage(
                new Uri($"{Environment.CurrentDirectory}\\Resources\\{key}\\{fileName}")
            );
        }

        /// <summary>
        /// Get ImageSource from corresponding Resources folder
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name">resource name with out filename extension</param>
        /// <returns></returns>
        public static ImageSource? GetImageSourceFromName(ImageSourceKey key, string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            if (!File.Exists($"{Environment.CurrentDirectory}\\Resources\\{key}\\{name}.png")) return null;


            return new BitmapImage(
                new Uri($"{Environment.CurrentDirectory}\\Resources\\{key}\\{name}.png")
            );
        }

        /// <summary>
        /// Get Talent ImageSource corresponding Resources folder
        /// </summary>
        /// <param name="camp"></param>
        /// <param name="name">Talent Name</param>
        /// <returns></returns>
        public static ImageSource? GetTalentImageSource(Camp camp, string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            if (!File.Exists($"{Environment.CurrentDirectory}\\Resources\\{ImageSourceKey.talent}\\{camp.ToString().ToLower()}\\{name}.png")) return null;

            return new BitmapImage(
                new Uri($"{Environment.CurrentDirectory}\\Resources\\{ImageSourceKey.talent}\\{camp.ToString().ToLower()}\\{name}.png")
                );
        }
    }
}
