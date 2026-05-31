using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Lists image resources for Designer v3 Resource Browser.
/// </summary>
public sealed class FrontedResourceBrowserProvider
{
    private static readonly string[] SupportedImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".bmp"
    ];

    private readonly string _resourcesRoot;

    public FrontedResourceBrowserProvider()
        : this(AppConstants.ResourcesPath)
    {
    }

    public FrontedResourceBrowserProvider(string resourcesRoot)
    {
        _resourcesRoot = resourcesRoot;
    }

    public IReadOnlyList<FrontedResourceBrowserItem> ListBuiltInResources()
    {
        var bpuiPath = Path.Combine(_resourcesRoot, "bpui");
        if (!Directory.Exists(bpuiPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(bpuiPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedImage)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => new FrontedResourceBrowserItem
            {
                DisplayName = Path.GetFileName(path),
                SelectedPath = $"Resources/{Path.GetFileName(path)}",
                FilePath = path,
                Category = "BuiltInResources",
                Thumbnail = LoadThumbnail(path)
            })
            .ToArray();
    }

    public IReadOnlyList<FrontedResourceBrowserItem> Search(string? query)
    {
        var resources = ListBuiltInResources();
        var filter = query?.Trim();
        if (string.IsNullOrWhiteSpace(filter))
        {
            return resources;
        }

        return resources
            .Where(resource =>
                resource.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || resource.SelectedPath.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public FrontedResourceBrowserItem CreateAbsoluteFileItem(string path) =>
        new()
        {
            DisplayName = Path.GetFileName(path),
            SelectedPath = path,
            FilePath = path,
            Category = "AbsoluteFile",
            Thumbnail = LoadThumbnail(path),
            IsAbsoluteFile = true
        };

    public static bool IsSupportedImage(string path) =>
        SupportedImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static ImageSource? LoadThumbnail(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = 96;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
