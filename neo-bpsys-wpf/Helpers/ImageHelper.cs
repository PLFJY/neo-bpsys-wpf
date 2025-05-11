using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Helpers
{
    /// <summary>
    /// 提供从资源目录加载图像资源的静态方法
    /// </summary>
    public static class ImageHelper
    {
        /// <summary>
        /// 从Resources\bpui\目录加载指定键名的UI图像并创建ImageBrush
        /// </summary>
        /// <param name="key">图像文件名（不含扩展名）</param>
        /// <returns>创建的ImageBrush对象，若加载失败返回null</returns>
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
        /// 从Resources\bpui\目录加载指定键名的UI图像并创建ImageSource
        /// </summary>
        /// <param name="key">图像文件名（不含扩展名）</param>
        /// <returns>创建的ImageSource对象，若加载失败返回null</returns>
        public static ImageSource? GetUiImageSource(string key)
        {
            return new BitmapImage(
                    new Uri(
                        $"{Environment.CurrentDirectory}\\Resources\\{ImageSourceKey.bpui}\\{key}.png"
                    )
            );
        }

        /// <summary>
        /// 从指定资源目录加载指定文件名的图像资源
        /// </summary>
        /// <param name="key">资源目录分类标识符</param>
        /// <param name="fileName">完整文件名（含扩展名），可为空</param>
        /// <returns>创建的ImageSource对象，若文件名为空或文件不存在返回null</returns>
        public static ImageSource? GetImageSourceFromFileName(ImageSourceKey key, string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            if (!File.Exists($"{Environment.CurrentDirectory}\\Resources\\{key}\\{fileName}")) return null;

            return new BitmapImage(
                new Uri($"{Environment.CurrentDirectory}\\Resources\\{key}\\{fileName}")
            );
        }

        /// <summary>
        /// 从指定资源目录加载指定名称的PNG图像资源
        /// </summary>
        /// <param name="key">资源目录分类标识符</param>
        /// <param name="name">图像名称（不含扩展名），可为空</param>
        /// <returns>创建的ImageSource对象，若名称为空或文件不存在返回null</returns>
        public static ImageSource? GetImageSourceFromName(ImageSourceKey key, string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            if (!File.Exists($"{Environment.CurrentDirectory}\\Resources\\{key}\\{name}.png")) return null;


            return new BitmapImage(
                new Uri($"{Environment.CurrentDirectory}\\Resources\\{key}\\{name}.png")
            );
        }

        /// <summary>
        /// 加载指定阵营和名称的天赋图像资源
        /// </summary>
        /// <param name="camp">天赋所属阵营</param>
        /// <param name="name">天赋名称（不含扩展名），可为空</param>
        /// <returns>创建的ImageSource对象，若名称为空或文件不存在返回null</returns>
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