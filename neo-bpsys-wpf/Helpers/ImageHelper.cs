using neo_bpsys_wpf.Enums;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Abstractions.Services;

namespace neo_bpsys_wpf.Helpers
{
    public static class ImageHelper
    {
        /// <summary>
        /// Get Ui ImageBrush from Resources\bpui\
        /// </summary>
        /// <param name="key">ui _image filename without filename extension</param>
        /// <returns></returns>
        public static ImageBrush GetUiImageBrush(string key)
        {
            return new ImageBrush(
                new BitmapImage(
                    new Uri(
                        Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory, "Resources", nameof(ImageSourceKey.bpui),
                            key + ".png"
                        )
                    )
                )
            );
        }

        /// <summary>
        /// Get Ui ImageSource from Resources\bpui\
        /// </summary>
        /// <param name="key">ui _image filename without filename extension</param>
        /// <returns></returns>
        public static ImageSource GetUiImageSource(string key)
        {
            return new BitmapImage(
                new Uri(
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, "Resources", nameof(ImageSourceKey.bpui), key + ".png"
                    )
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

            var fileFullName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", key.ToString(), fileName);

            return !File.Exists(fileFullName) ? null : new BitmapImage(new Uri(fileFullName));
        }

        /// <summary>
        /// Get ImageSource from corresponding Resources folder
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name">resource name without filename extension</param>
        /// <returns></returns>
        public static ImageSource? GetImageSourceFromName(ImageSourceKey key, string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", key.ToString(), name + ".png");

            return !File.Exists(fileName) ? null : new BitmapImage(new Uri(fileName));
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

            var isBlackVerEnable = SettingsHostService.Value.Settings.CutSceneWindowSettings
                .IsBlackTalentAndTraitEnable;

            var fileName = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources",
                nameof(ImageSourceKey.talent),
                camp.ToString().ToLower(),
                isBlackVerEnable ? "black" : "white",
                name + ".png"
            );

            return !File.Exists(fileName) ? null : new BitmapImage(new Uri(fileName));
        }

        private static readonly Lazy<ISettingsHostService> SettingsHostService =
            new(() => App.Services.GetRequiredService<ISettingsHostService>());

        public static ImageSource? GetTraitImageSource(Trait? trait)
        {
            if (trait == null) return null;
            var isBlackVerEnable = SettingsHostService.Value.Settings.CutSceneWindowSettings
                .IsBlackTalentAndTraitEnable;
            var fileName = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources",
                nameof(ImageSourceKey.trait),
                (isBlackVerEnable ? "black" : "white"),
                trait + ".png"
            );
            return !File.Exists(fileName) ? null : new BitmapImage(new Uri(fileName));
        }
    }
}