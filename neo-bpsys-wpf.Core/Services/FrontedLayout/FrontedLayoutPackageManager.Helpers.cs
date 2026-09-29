#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台布局包管理器的Helpers逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageManager
{
    private static string GenerateSafePackageIdFromName(string name, int suffix)
    {
        var safe = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9._-]+", "-").Trim('-', '.', '_');
        if (string.IsNullOrWhiteSpace(safe) || !char.IsAsciiLetterOrDigit(safe[0]) || !IsSafePackageId(safe))
        {
            safe = $"user-layout-scheme-{suffix}";
        }

        return safe;
    }

    private static bool IsSafeRelativePath(string relativePath)
    {
        return relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .All(segment => segment is not ("." or "..") && !string.IsNullOrWhiteSpace(segment));
    }

    private async Task<FrontedLayoutPackageManifest> ReadManifestAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(packagePath, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Package manifest is missing.", manifestPath);
        }

        if (new FileInfo(manifestPath).Length > FrontedLayoutLimits.MaxManifestBytes)
        {
            throw new InvalidDataException("Package manifest is too large.");
        }

        var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        var manifest = JsonSerializer.Deserialize<FrontedLayoutPackageManifest>(json, _jsonSerializerOptions)
            ?? throw new InvalidDataException("Package manifest must be a JSON object.");
        manifest.Content ??= new FrontedLayoutPackageManifestContent();
        manifest.Content.Layouts ??= [];
        manifest.Content.CustomWindows ??= [];
        manifest.Content.Resources ??= [];
        return manifest;
    }

    private async Task WriteConfigAsync(
        string path,
        FrontedWindowConfig config,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        config.Version = 3;
        var json = JsonSerializer.Serialize(config, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private static string GetBehaviorPath(string packagePath, string canonicalWindowId)
    {
        var relativeLayoutPath = FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(canonicalWindowId);
        var folder = Path.GetDirectoryName(relativeLayoutPath);
        var fileName = $"{Path.GetFileNameWithoutExtension(relativeLayoutPath)}.behaviors.json";
        var relativePath = string.IsNullOrWhiteSpace(folder)
            ? Path.Combine("FrontedBehaviors", fileName)
            : Path.Combine("FrontedBehaviors", folder, fileName);
        return CombineInsideRoot(packagePath, relativePath);
    }

    private static string CombineInsideRoot(string root, string relativePath)
    {
        var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Package path escaped its root.");
        }

        return candidate;
    }

    private static string ResolveCustomDisplayName(
        IReadOnlyDictionary<string, string> displayNames,
        string windowId)
    {
        foreach (var language in new[] { "zh_Hans", "en_US", "ja_JP" })
        {
            if (displayNames.TryGetValue(language, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return windowId;
    }

    private static bool IsReservedPackageEntry(string name)
    {
        return string.Equals(name, BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, LocalPackageId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, ActivePackageFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static FrontedLayoutActivePackageState CreateBuiltInActiveState()
    {
        return new FrontedLayoutActivePackageState
        {
            PackageId = BuiltInPackageId,
            ActivatedAt = DateTimeOffset.MinValue
        };
    }

    /// <summary>
    /// 验证 PackageId 是否安全（仅包含字母数字和 <c>._-</c> 字符，不含 <c>..</c> 或 <c>%</c>）。
    /// </summary>
    /// <param name="packageId">待验证的包 ID。</param>
    /// <returns>是否安全。</returns>
    public static bool IsSafePackageId(string packageId)
    {
        return !string.IsNullOrWhiteSpace(packageId)
               && SafePackageIdRegex.IsMatch(packageId)
               && !packageId.Contains("..", StringComparison.Ordinal)
               && !packageId.Contains('%', StringComparison.Ordinal)
               && FrontedV3LayoutWindowPathHelper.IsSafePathSegment(packageId);
    }

    private static void EnsureSafePackageId(string packageId)
    {
        if (!IsSafePackageId(packageId))
        {
            throw new ArgumentException("PackageId is not safe.", nameof(packageId));
        }
    }

    private static int CountFiles(string directory, string pattern)
    {
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories).Count()
            : 0;
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static void AppendWarning(FrontedLayoutPackageInfo info, string warning)
    {
        info.ValidationStatus = FrontedLayoutPackageValidationStatus.Warning;
        info.ValidationMessage = string.IsNullOrWhiteSpace(info.ValidationMessage)
            ? warning
            : $"{info.ValidationMessage} {warning}";
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
