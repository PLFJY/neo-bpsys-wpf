using neo_bpsys_wpf.Core.Abstractions.Services;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// File-backed editor-local bpui resource store.
/// </summary>
public class FrontedLocalResourceStore : IFrontedLocalResourceStore
{
    private static readonly Regex UnsafeFileNameChars = new("[^A-Za-z0-9._-]+", RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
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

    private readonly string _imagesFolder;
    private readonly IFrontedImageSafetyService _imageSafetyService;

    public FrontedLocalResourceStore()
        : this(AppConstants.FrontedLayoutLocalImagesPath)
    {
    }

    public FrontedLocalResourceStore(IFrontedImageSafetyService imageSafetyService)
        : this(AppConstants.FrontedLayoutLocalImagesPath, imageSafetyService)
    {
    }

    public FrontedLocalResourceStore(string imagesFolder)
        : this(imagesFolder, new FrontedImageSafetyService())
    {
    }

    public FrontedLocalResourceStore(string imagesFolder, IFrontedImageSafetyService imageSafetyService)
    {
        _imagesFolder = imagesFolder;
        _imageSafetyService = imageSafetyService;
    }

    public string StoreImage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source image path is required.", nameof(sourcePath));
        }

        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("Source image file was not found.", fullSourcePath);
        }

        var extension = Path.GetExtension(fullSourcePath);
        if (!SupportedImageExtensions.Contains(extension))
        {
            throw new NotSupportedException($"Unsupported image extension: {extension}");
        }

        var validation = _imageSafetyService.ValidateFile(fullSourcePath, FrontedImagePurpose.Background);
        if (!validation.IsValid)
        {
            throw validation.ErrorCode switch
            {
                "ImageTooLarge" => new InvalidDataException("ImageTooLarge"),
                "ImageTooManyPixels" => new InvalidDataException("ImageTooManyPixels"),
                "UnsupportedImageFormat" => new NotSupportedException("UnsupportedImageFormat"),
                _ => new InvalidDataException(validation.ErrorMessage ?? "Image validation failed.")
            };
        }

        Directory.CreateDirectory(_imagesFolder);

        var hash = ComputeSha256(fullSourcePath);
        var fileName = CreateFileName(Path.GetFileNameWithoutExtension(fullSourcePath), hash, extension);
        var targetPath = Path.Combine(_imagesFolder, fileName);

        if (File.Exists(targetPath))
        {
            var existingHash = ComputeSha256(targetPath);
            if (!string.Equals(existingHash, hash, StringComparison.OrdinalIgnoreCase))
            {
                fileName = CreateFileName(Path.GetFileNameWithoutExtension(fullSourcePath), hash, extension, forceHashOnly: true);
                targetPath = Path.Combine(_imagesFolder, fileName);
            }
        }

        if (!File.Exists(targetPath))
        {
            File.Copy(fullSourcePath, targetPath, overwrite: false);
        }

        return $"bpui://local/resources/images/{fileName}";
    }

    private static string CreateFileName(string originalName, string hash, string extension, bool forceHashOnly = false)
    {
        var safeBaseName = UnsafeFileNameChars.Replace(originalName, "-")
            .Replace("..", "-", StringComparison.Ordinal);
        while (safeBaseName.Contains("--", StringComparison.Ordinal))
        {
            safeBaseName = safeBaseName.Replace("--", "-", StringComparison.Ordinal);
        }

        safeBaseName = safeBaseName.Trim('.', '-', '_');
        if (string.IsNullOrWhiteSpace(safeBaseName) || forceHashOnly)
        {
            safeBaseName = "image";
        }

        var shortHash = hash[..12];
        return $"{safeBaseName}-{shortHash}{extension.ToLowerInvariant()}";
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }
}
