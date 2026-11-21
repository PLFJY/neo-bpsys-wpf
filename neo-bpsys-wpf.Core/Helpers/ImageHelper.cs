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
        if (!TryResolveResourcePath(nameof(ImageSourceKey.bpui), key + ".png", out var fileFullName))
            return null;
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
        if (!TryResolveResourcePath(key.ToString(), fileName, out var fileFullName)) return null;
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
        if (!TryResolveResourcePath(key, fileName, out var fileFullName)) return null;
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
        if (!TryResolveResourcePath(key.ToString(), name + ".png", out var fileName)) return null;

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

        if (!TryResolveResourcePath(
                Path.Combine(nameof(ImageSourceKey.talent), camp.ToString().ToLower(), isBlackVerEnable ? "black" : "white"),
                name + ".png",
                out var fileName))
        {
            return null;
        }

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

        if (!TryResolveResourcePath(
                Path.Combine(nameof(ImageSourceKey.trait), isBlackTalentAndTraitEnable ? "black" : "white"),
                trait + ".png",
                out var fileName))
        {
            return null;
        }

        return !File.Exists(fileName) ? null : new BitmapImage(new Uri(fileName));
    }

    /// <summary>
    /// 将 Resources 下的相对根目录与文件名解析为固定资源根目录内的完整路径。
    /// </summary>
    /// <param name="relativeRoot">相对资源分类目录，例如 <c>talent/sur/black</c>。</param>
    /// <param name="relativeFile">相对文件名，不允许绝对路径或 <c>..</c> 段。</param>
    /// <param name="fullPath">解析成功时的完整路径。</param>
    /// <returns>路径位于 Resources 根目录内且格式安全时返回 <see langword="true"/>。</returns>
    internal static bool TryResolveResourcePath(string relativeRoot, string relativeFile, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativeRoot)
            || string.IsNullOrWhiteSpace(relativeFile)
            || Path.IsPathRooted(relativeFile)
            || relativeFile.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || relativeRoot.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return false;
        }

        var normalizedRoot = relativeRoot
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var normalizedFile = relativeFile
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        if (normalizedRoot.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment == "..")
            || normalizedFile.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment == ".." || segment == "."))
        {
            return false;
        }

        try
        {
            var resourceRoot = Path.GetFullPath(AppConstants.ResourcesPath);
            var rootWithSeparator = resourceRoot.TrimEnd(Path.DirectorySeparatorChar)
                                    + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(resourceRoot, normalizedRoot, normalizedFile));
            if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试加载受信任应用目录内的本地图片。
    /// </summary>
    /// <param name="source">file URI 或本地绝对路径。</param>
    /// <param name="image">成功时的冻结图片源。</param>
    /// <returns>路径属于受信任应用目录且图片可解码时返回 <see langword="true"/>。</returns>
    /// <remarks>
    /// 队伍/选手数据可来自导入文件；禁止其把任意本地路径当作展示图片读取。
    /// </remarks>
    internal static bool TryLoadTrustedLocalImage(string? source, out ImageSource? image)
    {
        image = null;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        string? localPath = null;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            if (!string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            localPath = uri.LocalPath;
        }
        else if (Path.IsPathRooted(source))
        {
            localPath = source;
        }

        if (string.IsNullOrWhiteSpace(localPath) || !IsUnderTrustedImageRoot(localPath))
        {
            return false;
        }

        try
        {
            var bitmap = new BitmapImage(new Uri(Path.GetFullPath(localPath)));
            bitmap.Freeze();
            image = bitmap;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUnderTrustedImageRoot(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }

        foreach (var root in new[]
                 {
                     AppConstants.ResourcesPath,
                     AppConstants.AppDataPath,
                     AppConstants.AppOutputPath,
                     AppContext.BaseDirectory
                 })
        {
            try
            {
                var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                                     + Path.DirectorySeparatorChar;
                if (fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // 忽略无效根路径。
            }
        }

        return false;
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