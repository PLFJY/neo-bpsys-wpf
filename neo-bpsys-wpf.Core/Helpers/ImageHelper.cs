using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Enums;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 图片工具类
/// </summary>
public static class ImageHelper
{
    private static ILogger? Logger => IAppHost.TryGetService<ILogger>();

    /// <summary>
    /// Get Ui ImageBrush from Resources\bpui\
    /// </summary>
    /// <param name="key">ui _image filename without filename extension</param>
    /// <returns></returns>
    public static ImageBrush? GetUiImageBrush(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        var image = GetUiImageSource(key);
        if(image == null) return null;
        return new ImageBrush(image);
    }

    /// <summary>
    /// Get Ui ImageSource from Resources\bpui\
    /// </summary>
    /// <param name="key">ui _image filename without filename extension</param>
    /// <returns></returns>
    public static ImageSource? GetUiImageSource(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        var fileFullName = Path.Combine(AppConstants.ResourcesPath, nameof(ImageSourceKey.bpui), key + ".png");
        if (!File.Exists(fileFullName)) return null;
        var image = new BitmapImage(new Uri(fileFullName));
        image.Freeze();
        return image;
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

        var fileFullName = Path.Combine(AppConstants.ResourcesPath, key.ToString(), fileName);
        if (!File.Exists(fileFullName)) return null;
        var image = new BitmapImage(new Uri(fileFullName));
        image.Freeze();

        return image;
    }

    /// <summary>
    /// Get ImageSource from corresponding Resources folder
    /// </summary>
    /// <param name="key">ImageSourceKey</param>
    /// <param name="fileName">file name</param>
    /// <returns></returns>
    public static ImageSource? GetImageSourceFromFileName(string key, string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;

        var fileFullName = Path.Combine(AppConstants.ResourcesPath, key, fileName);
        if(!File.Exists(fileFullName)) return null;
        var image = new BitmapImage(new Uri(fileFullName));
        image.Freeze();

        return image;
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

        var fileName = Path.Combine(AppConstants.ResourcesPath, key.ToString(), name + ".png");

        return !File.Exists(fileName) ? null : new BitmapImage(new Uri(fileName));
    }

    /// <summary>
    /// Get Talent ImageSource corresponding Resources folder
    /// </summary>
    /// <param name="camp"></param>
    /// <param name="name">Talent Name</param>
    /// <param name="isBlackVerEnable">Is Black Ver Enable</param>
    /// <returns></returns>
    public static ImageSource? GetTalentImageSource(Camp camp, string? name, bool isBlackVerEnable)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var fileName = Path.Combine(
            AppConstants.ResourcesPath,
            nameof(ImageSourceKey.talent),
            camp.ToString().ToLower(),
            isBlackVerEnable ? "black" : "white",
            name + ".png"
        );

        return !File.Exists(fileName) ? null : new BitmapImage(new Uri(fileName));
    }

    /// <summary>
    /// Get Trait ImageSource corresponding Resources folder
    /// </summary>
    /// <param name="trait">Trait</param>
    /// <param name="isBlackTalentAndTraitEnable">Is Black Ver Enable</param>
    /// <returns></returns>
    public static ImageSource? GetTraitImageSource(TraitType? trait, bool isBlackTalentAndTraitEnable)
    {
        if (trait == null) return null;

        var fileName = Path.Combine(
            AppConstants.ResourcesPath,
            nameof(ImageSourceKey.trait),
            (isBlackTalentAndTraitEnable ? "black" : "white"),
            trait + ".png"
        );
        return !File.Exists(fileName) ? null : new BitmapImage(new Uri(fileName));
    }

    /// <summary>
    /// 获取Ui图片
    /// </summary>
    /// <param name="uriStr">图片uri</param>
    /// <returns></returns>
    public static ImageSource? GetImageFromUriStr(string? uriStr)
    {
        if (string.IsNullOrEmpty(uriStr)) return null;
        uriStr = Environment.ExpandEnvironmentVariables(uriStr);

        try
        {
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            using (Stream ms = new MemoryStream(File.ReadAllBytes(uriStr)))
            {
                bitmapImage.StreamSource = ms;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }
            return bitmapImage;
        }
        catch (Exception ex)
        {
            // ignored
            Logger?.LogWarning(ex, "Failed to load image from {Uri}", uriStr);
#if DEBUG
            MessageBox.Show(ex.Message);
#endif
        }

        return null;
    }

    /// <summary>
    /// 获取Ui图片
    /// </summary>
    /// <param name="uriStr">图片uri</param>
    /// <param name="defaultKey">默认图片key</param>
    /// <returns></returns>
    public static ImageSource? GetUiImageFromSetting(string? uriStr, string defaultKey)
    {
        return string.IsNullOrEmpty(uriStr) ? GetUiImageSource(defaultKey) : GetImageFromUriStr(uriStr);
    }



}