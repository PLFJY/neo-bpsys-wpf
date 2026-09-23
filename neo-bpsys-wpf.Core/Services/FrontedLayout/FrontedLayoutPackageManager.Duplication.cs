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
/// 前台布局包管理器的Duplication逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageManager
{
    private async Task<FrontedLayoutPackageManifest> CreateDuplicateManifestAsync(
        string packagePath,
        string packageId,
        string displayName,
        string sourcePackageId,
        CancellationToken cancellationToken)
    {
        FrontedLayoutPackageManifest? sourceManifest = null;
        var manifestPath = Path.Combine(packagePath, ManifestFileName);
        if (!string.Equals(sourcePackageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
            && File.Exists(manifestPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                sourceManifest = JsonSerializer.Deserialize<FrontedLayoutPackageManifest>(json, _jsonSerializerOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read source package manifest while duplicating {SourcePackageId}.", sourcePackageId);
            }
        }

        var manifest = sourceManifest ?? new FrontedLayoutPackageManifest();
        manifest.PackageId = packageId;
        manifest.Name = displayName;
        manifest.Description = LocalizedOrFallback("UserLayoutSchemeDescription", "User editable layout scheme.");
        manifest.CreatedAt = DateTimeOffset.UtcNow;
        manifest.CreatedVersion = AppConstants.AppVersion;
        manifest.Format = "neo-bpsys-bpui";
        manifest.FormatVersion = 3;
        manifest.LayoutSchemaVersion = 3;
        manifest.Content ??= new FrontedLayoutPackageManifestContent();
        if (!string.Equals(sourcePackageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            await RemapDuplicatedCustomWindowsAsync(
                packagePath,
                sourcePackageId,
                packageId,
                cancellationToken);
            await RewriteDuplicatedPackageResourceUrisAsync(
                packagePath,
                sourcePackageId,
                packageId,
                cancellationToken);
        }

        manifest.Content.Layouts = EnumerateLayoutEntries(packagePath)
            .Where(entry => !FrontedV3LayoutWindowPathHelper.TryParseCustomCanonicalWindowId(
                entry.Window, out _, out _))
            .ToList();
        manifest.Content.CustomWindows = EnumerateLayoutEntries(packagePath)
            .Where(entry => FrontedV3LayoutWindowPathHelper.TryParseCustomCanonicalWindowId(
                entry.Window, out _, out _))
            .ToList();
        if (manifest.Content.Resources.Count == 0)
        {
            manifest.Content.Resources = EnumerateResourceEntries(packagePath).ToList();
        }
        else
        {
            foreach (var resource in manifest.Content.Resources)
            {
                resource.Id = RewritePackageResourceUri(resource.Id, sourcePackageId, packageId);
                resource.Uri = RewritePackageResourceUri(resource.Uri, sourcePackageId, packageId);
            }
        }
        return manifest;
    }

    private async Task RemapDuplicatedCustomWindowsAsync(
        string packagePath,
        string sourcePackageId,
        string targetPackageId,
        CancellationToken cancellationToken)
    {
        var customEntries = EnumerateLayoutEntries(packagePath)
            .Where(entry => FrontedV3LayoutWindowPathHelper.TryParseCustomCanonicalWindowId(
                entry.Window, out var packageId, out _)
                && string.Equals(packageId, sourcePackageId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var entry in customEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!FrontedV3LayoutWindowPathHelper.TryParseCustomCanonicalWindowId(
                    entry.Window, out _, out var localWindowId))
            {
                continue;
            }

            var targetWindowId = $"{FrontedV3LayoutWindowPathHelper.CustomPrefix}{targetPackageId}/{localWindowId}";
            var sourceLayoutPath = CombineInsideRoot(packagePath, entry.Path);
            var targetLayoutPath = CombineInsideRoot(
                packagePath,
                Path.Combine("FrontedLayouts", FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(targetWindowId)));
            if (!string.Equals(sourceLayoutPath, targetLayoutPath, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetLayoutPath)!);
                File.Move(sourceLayoutPath, targetLayoutPath, overwrite: true);
            }

            var sourceBehaviorPath = GetBehaviorPath(packagePath, entry.Window);
            var targetBehaviorPath = GetBehaviorPath(packagePath, targetWindowId);
            if (File.Exists(sourceBehaviorPath)
                && !string.Equals(sourceBehaviorPath, targetBehaviorPath, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetBehaviorPath)!);
                File.Move(sourceBehaviorPath, targetBehaviorPath, overwrite: true);
            }

            if (File.Exists(targetBehaviorPath))
            {
                var behaviorNode = JsonNode.Parse(await File.ReadAllTextAsync(targetBehaviorPath, cancellationToken));
                if (behaviorNode is JsonObject behaviorObject)
                {
                    behaviorObject["WindowType"] = targetWindowId;
                    await File.WriteAllTextAsync(
                        targetBehaviorPath,
                        behaviorNode.ToJsonString(_jsonSerializerOptions),
                        cancellationToken);
                }
            }
        }
    }

    private async Task RewriteDuplicatedPackageResourceUrisAsync(
        string packagePath,
        string sourcePackageId,
        string targetPackageId,
        CancellationToken cancellationToken)
    {
        foreach (var folderName in new[] { "FrontedLayouts", "FrontedBehaviors" })
        {
            var root = Path.Combine(packagePath, folderName);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var node = JsonNode.Parse(await File.ReadAllTextAsync(file, cancellationToken));
                if (node is null || !RewritePackageResourceUris(node, sourcePackageId, targetPackageId))
                {
                    continue;
                }

                await File.WriteAllTextAsync(
                    file,
                    node.ToJsonString(_jsonSerializerOptions),
                    cancellationToken);
            }
        }
    }

    private static bool RewritePackageResourceUris(
        JsonNode node,
        string sourcePackageId,
        string targetPackageId)
    {
        var changed = false;
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    var rewritten = RewritePackageResourceUri(text, sourcePackageId, targetPackageId);
                    if (!string.Equals(rewritten, text, StringComparison.Ordinal))
                    {
                        obj[property.Key] = rewritten;
                        changed = true;
                    }
                }
                else if (property.Value is not null)
                {
                    changed |= RewritePackageResourceUris(property.Value, sourcePackageId, targetPackageId);
                }
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                if (array[index] is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    var rewritten = RewritePackageResourceUri(text, sourcePackageId, targetPackageId);
                    if (!string.Equals(rewritten, text, StringComparison.Ordinal))
                    {
                        array[index] = rewritten;
                        changed = true;
                    }
                }
                else if (array[index] is { } child)
                {
                    changed |= RewritePackageResourceUris(child, sourcePackageId, targetPackageId);
                }
            }
        }

        return changed;
    }

    private static string RewritePackageResourceUri(
        string value,
        string sourcePackageId,
        string targetPackageId)
    {
        var sourcePrefix = $"bpui://{sourcePackageId}/";
        return value.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)
            ? $"bpui://{targetPackageId}/{value[sourcePrefix.Length..]}"
            : value;
    }

}
