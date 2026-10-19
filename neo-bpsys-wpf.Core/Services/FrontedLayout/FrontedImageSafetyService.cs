using System.IO;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public enum FrontedImagePurpose
{
    Background,
    UiElement,
    PackageResource,
    PreviewThumbnail
}

public sealed class FrontedImageValidationResult
{
    public bool IsValid { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public long FileBytes { get; init; }

    public int PixelWidth { get; init; }

    public int PixelHeight { get; init; }
}

public interface IFrontedImageSafetyService
{
    FrontedImageValidationResult ValidateFile(
        string path,
        FrontedImagePurpose purpose,
        bool knownBackgroundImage = false,
        bool knownUiImage = false);
}

public sealed class FrontedImageSafetyService : IFrontedImageSafetyService
{
    private const int MaxCachedImageHeaders = 1024;
    private readonly Dictionary<string, ImageHeader> _imageHeaders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _imageHeaderOrder = new();
    private sealed record ImageHeader(long FileBytes, DateTime LastWriteTimeUtc, DateTime CreationTimeUtc, int Width, int Height);
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".webp",
        ".ico",
        ".tif",
        ".tiff"
    };

    public FrontedImageValidationResult ValidateFile(
        string path,
        FrontedImagePurpose purpose,
        bool knownBackgroundImage = false,
        bool knownUiImage = false)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Invalid("ImageNotFound", "Image file was not found.");
        }

        var extension = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(extension))
        {
            return Invalid("UnsupportedImageFormat", "Unsupported image format.");
        }

        var info = new FileInfo(path);
        var fileBytes = info.Length;
        var lastWriteTime = info.LastWriteTimeUtc;
        var creationTime = info.CreationTimeUtc;
        var (maxBytes, maxLongSide, maxPixels) = GetLimits(purpose, knownBackgroundImage, knownUiImage);
        if (fileBytes > maxBytes)
        {
            return Invalid("ImageTooLarge", "Image file is too large.", fileBytes: fileBytes);
        }

        lock (_imageHeaders)
        {
            if (_imageHeaders.TryGetValue(info.FullName, out var header)
                && header.FileBytes == fileBytes && header.LastWriteTimeUtc == lastWriteTime && header.CreationTimeUtc == creationTime)
            {
                return ValidateDimensions(fileBytes, header.Width, header.Height, maxLongSide, maxPixels);
            }
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.None);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame is null)
            {
                return Invalid("UnsupportedImageFormat", "Unsupported image format.", fileBytes: info.Length);
            }

            var width = frame.PixelWidth;
            var height = frame.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                return Invalid("UnsupportedImageFormat", "Unsupported image dimensions.", fileBytes: info.Length);
            }

            info.Refresh();
            if (info.Exists && info.Length == fileBytes && info.LastWriteTimeUtc == lastWriteTime && info.CreationTimeUtc == creationTime)
            {
                lock (_imageHeaders)
                {
                    if (!_imageHeaders.ContainsKey(info.FullName)) _imageHeaderOrder.Enqueue(info.FullName);
                    _imageHeaders[info.FullName] = new ImageHeader(fileBytes, lastWriteTime, creationTime, width, height);
                    while (_imageHeaders.Count > MaxCachedImageHeaders)
                        _imageHeaders.Remove(_imageHeaderOrder.Dequeue());
                }
            }

            return ValidateDimensions(fileBytes, width, height, maxLongSide, maxPixels);
        }
        catch (Exception ex)
        {
            return Invalid("UnsupportedImageFormat", ex.Message, fileBytes: info.Length);
        }
    }

    private static FrontedImageValidationResult ValidateDimensions(long fileBytes, int width, int height, int maxLongSide, long maxPixels)
    {
        if (Math.Max(width, height) > maxLongSide || (long)width * height > maxPixels)
            return Invalid("ImageTooManyPixels", "Image dimensions are too large.", fileBytes, width, height);
        return new FrontedImageValidationResult { IsValid = true, FileBytes = fileBytes, PixelWidth = width, PixelHeight = height };
    }

    private static (long MaxBytes, int MaxLongSide, long MaxPixels) GetLimits(
        FrontedImagePurpose purpose,
        bool knownBackgroundImage,
        bool knownUiImage)
    {
        if (purpose == FrontedImagePurpose.Background || knownBackgroundImage)
        {
            return (
                FrontedLayoutLimits.MaxBackgroundImageBytes,
                FrontedLayoutLimits.MaxBackgroundImageLongSide,
                FrontedLayoutLimits.MaxBackgroundImagePixels);
        }

        if (purpose == FrontedImagePurpose.PackageResource && !knownUiImage)
        {
            return (
                FrontedLayoutLimits.MaxPackageSingleEntryBytes,
                FrontedLayoutLimits.MaxBackgroundImageLongSide,
                FrontedLayoutLimits.MaxBackgroundImagePixels);
        }

        return (
            FrontedLayoutLimits.MaxUiImageBytes,
            FrontedLayoutLimits.MaxUiImageLongSide,
            FrontedLayoutLimits.MaxUiImagePixels);
    }

    private static FrontedImageValidationResult Invalid(
        string code,
        string message,
        long fileBytes = 0,
        int pixelWidth = 0,
        int pixelHeight = 0)
    {
        return new FrontedImageValidationResult
        {
            IsValid = false,
            ErrorCode = code,
            ErrorMessage = message,
            FileBytes = fileBytes,
            PixelWidth = pixelWidth,
            PixelHeight = pixelHeight
        };
    }
}
