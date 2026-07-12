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
    /// 从 Resources\bpui\ 获取 UI ImageBrush
    /// </summary>
    /// <param name="key">ui 图片文件名（不含扩展名）</param>
    /// <returns></returns>
    public static ImageBrush? GetUiImageBrush(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        var image = GetUiImageSource(key);
        if(image == null) return null;
        return new ImageBrush(image);
    }

    /// <summary>
    /// 从 Resources\bpui\ 获取 UI ImageSource
    /// </summary>
    /// <param name="key">ui 图片文件名（不含扩展名）</param>
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
    /// 从对应的 Resources 文件夹获取 ImageSource
    /// </summary>
    /// <param name="key">ImageSourceKey</param>
    /// <param name="fileName">文件名</param>
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
    /// 从对应的 Resources 文件夹获取 ImageSource
    /// </summary>
    /// <param name="key">ImageSourceKey</param>
    /// <param name="fileName">文件名</param>
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
    /// 从对应的 Resources 文件夹获取 ImageSource
    /// </summary>
    /// <param name="key"></param>
    /// <param name="name">资源名称（不含扩展名）</param>
    /// <returns></returns>
    public static ImageSource? GetImageSourceFromName(ImageSourceKey key, string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var fileName = Path.Combine(AppConstants.ResourcesPath, key.ToString(), name + ".png");

        return !File.Exists(fileName) ? null : new BitmapImage(new Uri(fileName));
    }

    /// <summary>
    /// 从对应的 Resources 文件夹获取天赋 ImageSource
    /// </summary>
    /// <param name="camp"></param>
    /// <param name="name">天赋名称</param>
    /// <param name="isBlackVerEnable">是否启用黑色版本</param>
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
    /// 从对应的 Resources 文件夹获取特质 ImageSource
    /// </summary>
    /// <param name="trait">特质</param>
    /// <param name="isBlackTalentAndTraitEnable">是否启用黑色版本</param>
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