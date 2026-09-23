#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Converters;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.Legacy;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Media;
using static neo_bpsys_wpf.Core.Services.FrontedLayout.LegacyConvertMessageHelper;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 旧版前台布局包转换器的Resources逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageLegacyConverter
{
    private ResourceConvertState CopyCustomUiResources(
        string? customUiRoot,
        string stagingRoot,
        string packageId,
        IReadOnlyDictionary<string, FrontedImagePurpose> imagePurposes,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        var state = new ResourceConvertState(packageId);
        if (string.IsNullOrWhiteSpace(customUiRoot) || !Directory.Exists(customUiRoot))
        {
            return state;
        }

        var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(customUiRoot));
        foreach (var file in Directory.EnumerateFiles(customUiRoot, "*", SearchOption.AllDirectories))
        {
            var fullFile = Path.GetFullPath(file);
            if (!fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Unsafe CustomUi file path: {file}");
            }

            var extension = Path.GetExtension(fullFile);
            var kind = ImageExtensions.Contains(extension) ? "Image" : "Other";
            var folder = kind == "Image" ? "images" : "other";
            var targetFolder = Path.Combine(stagingRoot, "resources", folder);
            Directory.CreateDirectory(targetFolder);
            var workingPath = Path.Combine(targetFolder, $".{Guid.NewGuid():N}{extension}");
            File.Copy(fullFile, workingPath, overwrite: false);

            FrontedImageCompressionResult? compression = null;
            if (kind == "Image"
                && imagePurposes.TryGetValue(Path.GetFileName(fullFile), out var purpose))
            {
                compression = _imageCompressionService.CompressIfNeeded(workingPath, purpose);
            }

            var sha256 = ComputeSha256(workingPath);
            var safeName = CreateResourceFileName(Path.GetFileNameWithoutExtension(fullFile), sha256, extension);
            var relativePath = ToZipPath("resources", folder, safeName);
            var targetPath = Path.Combine(stagingRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            File.Move(workingPath, targetPath, overwrite: false);
            var uri = $"bpui://{packageId}/{relativePath}";

            state.Add(fullFile, uri, relativePath, kind, sha256, safeName);
            messages.Add(Info(CodeResourceCopied,
                Args(new { FileName = Path.GetFileName(fullFile) })));
            if (compression?.WasCompressed == true)
            {
                messages.Add(Warning(CodeImageCompressed,
                    Args(new
                    {
                        FileName = Path.GetFileName(fullFile),
                        OriginalSize = FormatFileSize(compression.OriginalBytes),
                        CompressedSize = FormatFileSize(compression.CompressedBytes)
                    })));
            }
        }

        return state;
    }

    private static IReadOnlyDictionary<string, FrontedImagePurpose> ReadLegacyImagePurposes(
        ILegacyFrontendInputSource source)
    {
        var result = new Dictionary<string, FrontedImagePurpose>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(source.ConfigPath))
        {
            return result;
        }

        try
        {
            using var stream = source.OpenConfig();
            if (stream.Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                return result;
            }

            var root = JsonNode.Parse(
                stream,
                nodeOptions: null,
                documentOptions: new JsonDocumentOptions { MaxDepth = FrontedLayoutLimits.MaxJsonDepth });
            if (root is not JsonObject rootObject)
            {
                return result;
            }

            foreach (var (field, purpose) in LegacyConfigImagePurposes)
            {
                var separator = field.IndexOf('.', StringComparison.Ordinal);
                var sectionName = field[..separator];
                var propertyName = field[(separator + 1)..];
                if (rootObject[sectionName] is not JsonObject section
                    || section[propertyName] is not JsonValue value
                    || !value.TryGetValue<string>(out var configuredPath)
                    || string.IsNullOrWhiteSpace(configuredPath))
                {
                    continue;
                }

                var normalizedPath = Environment.ExpandEnvironmentVariables(configuredPath)
                    .Replace('\\', '/');
                var fileName = normalizedPath[(normalizedPath.LastIndexOf('/') + 1)..];
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                if (!result.TryGetValue(fileName, out var existing)
                    || purpose == FrontedImagePurpose.UiElement
                    || existing != FrontedImagePurpose.UiElement)
                {
                    result[fileName] = purpose;
                }
            }
        }
        catch (Exception)
        {
            // The existing Config reader reports the user-facing conversion warning.
        }

        return result;
    }

    private static string FormatFileSize(long bytes) =>
        bytes >= 1024L * 1024L
            ? $"{bytes / (1024D * 1024D):0.00} MiB"
            : $"{bytes / 1024D:0.0} KiB";

    private static IReadOnlyDictionary<string, string> ReadFrontendConfigValueMap(
        ILegacyFrontendInputSource source,
        ResourceConvertState resourceState,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(source.ConfigPath))
        {
            return result;
        }

        JsonNode? root;
        try
        {
            using var stream = source.OpenConfig();
            if (stream.Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                messages.Add(Warning(CodeConfigJsonTooLarge));
                return result;
            }

            root = JsonNode.Parse(
                stream,
                nodeOptions: null,
                documentOptions: new JsonDocumentOptions { MaxDepth = FrontedLayoutLimits.MaxJsonDepth });
        }
        catch (Exception ex)
        {
            messages.Add(Warning(CodeConfigJsonReadFailed,
                Args(new { Reason = ex.Message })));
            return result;
        }

        AddMappedImage(root, "BpWindowSettings", "BgImageUri", "BpWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "CutSceneWindowSettings", "BgUri", "CutSceneWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "ScoreWindowSettings", "SurScoreBgImageUri", "ScoreSurWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "ScoreWindowSettings", "HunScoreBgImageUri", "ScoreHunWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "ScoreWindowSettings", "GlobalScoreBgImageUri", "ScoreGlobalWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "ScoreWindowSettings", "GlobalScoreBgImageUriBo3", "ScoreGlobalWindow/BaseCanvas/BoModeStates/Bo3/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "GameDataWindowSettings", "BgImageUri", "GameDataWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "WidgetsWindowSettings", "MapBpBgUri", "WidgetsWindow/MapBpCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "WidgetsWindowSettings", "BpOverviewBgUri", "BpOverviewWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "WidgetsWindowSettings", "MapBpV2BgUri", "MapV2Window/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "BpWindowSettings", "CurrentBanLockImageUri", "BpWindow/BaseCanvas/CurrentBanLockImage", resourceState, result, messages);
        AddMappedImage(root, "BpWindowSettings", "GlobalBanLockImageUri", "BpWindow/BaseCanvas/GlobalBanLockImage", resourceState, result, messages);
        AddMappedImage(root, "BpWindowSettings", "PickingBorderImageUri", "BpWindow/BaseCanvas/PickingBorderImage", resourceState, result, messages);
        AddMappedValue(root, "BpWindowSettings", "PickingBorderColor", "BpWindow/BaseCanvas/PickingBorderColor", result);
        AddMappedImage(root, "WidgetsWindowSettings", "CurrentBanLockImageUri", "BpOverviewWindow/BaseCanvas/CurrentBanLockImage", resourceState, result, messages);
        AddMappedImage(root, "WidgetsWindowSettings", "GlobalBanLockImageUri", "BpOverviewWindow/BaseCanvas/GlobalBanLockImage", resourceState, result, messages);
        AddMappedImage(root, "WidgetsWindowSettings", "MapBpV2PickingBorderImageUri", "MapV2Window/BaseCanvas/MapBpV2PickingBorderImage", resourceState, result, messages);
        AddMappedValue(root, "WidgetsWindowSettings", "MapBpV2_PickingBorderColor", "MapV2Window/BaseCanvas/MapBpV2PickingBorderColor", result);

        foreach (var ignored in EnumeratePotentialFrontendImageFields(root)
                     .Where(field => !KnownConfigImageFields.Contains(field, StringComparer.Ordinal)))
        {
            messages.Add(Info(CodeLegacyFieldIgnored,
                Args(new { Field = ignored })));
        }

        return result;
    }

    private static void AddMappedImage(
        JsonNode? root,
        string settingsObject,
        string propertyName,
        string key,
        ResourceConvertState resourceState,
        IDictionary<string, string> result,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        var field = $"{settingsObject}.{propertyName}";
        var value = root?[settingsObject]?[propertyName]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (TryMapLegacyResourceValue(value, resourceState, out var uri))
        {
            result[key] = uri;
            return;
        }

        messages.Add(Info(CodeResourceMissing,
            Args(new { Field = field, Value = value })));
    }

    private static void AddMappedValue(
        JsonNode? root,
        string settingsObject,
        string propertyName,
        string key,
        IDictionary<string, string> result)
    {
        var value = root?[settingsObject]?[propertyName]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(value))
        {
            result[key] = value.Trim();
        }
    }

    private static readonly HashSet<string> KnownConfigImageFields =
    [
        "BpWindowSettings.BgImageUri",
        "CutSceneWindowSettings.BgUri",
        "ScoreWindowSettings.SurScoreBgImageUri",
        "ScoreWindowSettings.HunScoreBgImageUri",
        "ScoreWindowSettings.GlobalScoreBgImageUri",
        "ScoreWindowSettings.GlobalScoreBgImageUriBo3",
        "GameDataWindowSettings.BgImageUri",
        "WidgetsWindowSettings.MapBpBgUri",
        "WidgetsWindowSettings.BpOverviewBgUri",
        "WidgetsWindowSettings.MapBpV2BgUri",
        "BpWindowSettings.CurrentBanLockImageUri",
        "BpWindowSettings.GlobalBanLockImageUri",
        "BpWindowSettings.PickingBorderImageUri",
        "BpWindowSettings.PickingBorderColor",
        "WidgetsWindowSettings.CurrentBanLockImageUri",
        "WidgetsWindowSettings.GlobalBanLockImageUri",
        "WidgetsWindowSettings.MapBpV2PickingBorderImageUri",
        "WidgetsWindowSettings.MapBpV2_PickingBorderColor"
    ];

    private static IEnumerable<string> EnumeratePotentialFrontendImageFields(JsonNode? node)
    {
        if (node is not JsonObject root)
        {
            yield break;
        }

        foreach (var settings in root)
        {
            if (settings.Value is not JsonObject obj
                || !settings.Key.EndsWith("WindowSettings", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var property in obj)
            {
                if (property.Value is JsonValue value
                    && value.TryGetValue<string>(out _)
                    && (property.Key.EndsWith("Uri", StringComparison.Ordinal)
                        || property.Key.EndsWith("ImageUri", StringComparison.Ordinal)
                        || property.Key.EndsWith("Color", StringComparison.Ordinal)))
                {
                    yield return $"{settings.Key}.{property.Key}";
                }
            }
        }
    }

    private static void ApplyFrontendConfigValues(
        FrontedCanvasConfig config,
        LegacyLayoutMapping mapping,
        IReadOnlyDictionary<string, string> valueMap,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        var prefix = $"{mapping.TargetWindow}/{FrontedLayoutConstants.BaseCanvasName}/";
        if (valueMap.TryGetValue($"{prefix}BackgroundImage", out var background))
        {
            config.BackgroundImage = background;
        }

        if (valueMap.TryGetValue(
                $"{prefix}BoModeStates/Bo3/BackgroundImage",
                out var scoreGlobalBo3Background))
        {
            config.EnableBoModeStates = true;
            if (!config.BoModeStates.TryGetValue(
                    FrontedCanvasRuntimeStateResolver.Bo3StateKey,
                    out var bo3State))
            {
                bo3State = new FrontedCanvasStateConfig();
                config.BoModeStates[FrontedCanvasRuntimeStateResolver.Bo3StateKey] = bo3State;
            }

            bo3State.BackgroundImage = scoreGlobalBo3Background;
            messages.Add(Info(CodeBo3GlobalScoreBackgroundMapped));
        }

        if (mapping.TargetWindow == "BpWindow")
        {
            foreach (var blueprint in GetMappedControlBlueprints(mapping))
            {
                if (!config.Controls.TryGetValue(blueprint.TargetName ?? blueprint.LegacyName, out var rawControl)
                    || rawControl is not ImageFrontedControlConfig control)
                {
                    continue;
                }

                ApplyImageResourceOverride(control, blueprint, prefix, valueMap, messages);
            }
        }

        if (mapping.TargetWindow == "BpOverviewWindow")
        {
            foreach (var blueprint in GetMappedControlBlueprints(mapping))
            {
                if (!config.Controls.TryGetValue(blueprint.TargetName ?? blueprint.LegacyName, out var rawControl)
                    || rawControl is not ImageFrontedControlConfig control)
                {
                    continue;
                }

                ApplyImageResourceOverride(control, blueprint, prefix, valueMap, messages);
            }
        }

        if (mapping.TargetWindow == "MapV2Window")
        {
            foreach (var blueprint in GetMappedControlBlueprints(mapping))
            {
                if (!config.Controls.TryGetValue(blueprint.TargetName ?? blueprint.LegacyName, out var rawControl)
                    || rawControl is not MapV2DisplayControlConfig control)
                {
                    continue;
                }

                if (blueprint.SpecialProperties.TryGetValue("PickingBorderImageResourceSourceKey", out var imageKey)
                    && valueMap.TryGetValue($"{prefix}{imageKey}", out var borderUri))
                {
                    control.PickingBorderImagePath = borderUri;
                }

                if (blueprint.SpecialProperties.TryGetValue("PickingBorderColorResourceSourceKey", out var colorKey)
                    && valueMap.TryGetValue($"{prefix}{colorKey}", out var borderColor))
                {
                    control.PickingBorderFillColor = borderColor;
                }
            }
        }
    }

    private static IEnumerable<LegacyControlBlueprint> GetMappedControlBlueprints(LegacyLayoutMapping mapping)
    {
        if (!LegacyControlBlueprints.TryGetValue(
                new LegacyLayoutKey(mapping.SourceWindow, mapping.SourceCanvas),
                out var blueprints))
        {
            return [];
        }

        return blueprints.Where(blueprint => blueprint.Status == LegacyControlBlueprintStatus.Mapped);
    }

    private static void ApplyImageResourceOverride(
        ImageFrontedControlConfig control,
        LegacyControlBlueprint blueprint,
        string prefix,
        IReadOnlyDictionary<string, string> valueMap,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (control.PickingBorderAvailable
            && blueprint.SpecialProperties.TryGetValue("PickingBorderFillColorResourceSourceKey", out var colorSourceKey)
            && valueMap.TryGetValue($"{prefix}{colorSourceKey}", out var color))
        {
            control.PickingBorderFillColor = color;
        }

        if (blueprint.ResourceSourceKey is null)
        {
            return;
        }

        var key = $"{prefix}{blueprint.ResourceSourceKey}";
        if (!valueMap.TryGetValue(key, out var uri))
        {
            return;
        }

        switch (blueprint.ResourceSourceKey)
        {
            case "CurrentBanLockImage":
            case "GlobalBanLockImage":
                if (control.Lockable)
                {
                    control.LockImagePath = uri;
                    messages.Add(Info(CodeLockImageMapped,
                        Args(new { Key = key })));
                }

                break;
            case "PickingBorderImage":
                if (control.PickingBorderAvailable)
                {
                    control.PickingBorderImagePath = uri;
                    messages.Add(Info(CodePickingBorderImageMapped,
                        Args(new { Key = key })));
                }

                break;
        }

    }

    private static void RewriteKnownResourceStrings(
        FrontedCanvasConfig config,
        ResourceConvertState resourceState)
    {
        var node = JsonSerializer.SerializeToNode(config) ?? throw new InvalidOperationException("Layout could not be serialized.");
        RewriteResourceStrings(node, resourceState, null);
        var converted = node.Deserialize<FrontedCanvasConfig>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (converted is null)
        {
            return;
        }

        config.BackgroundImage = converted.BackgroundImage;
        config.EnableBoModeStates = converted.EnableBoModeStates;
        config.BoModeStates = converted.BoModeStates;
        config.Controls = converted.Controls;
    }

    private static void RewriteResourceStrings(JsonNode node, ResourceConvertState resourceState, string? propertyName)
    {
        if (node is JsonObject obj)
        {
            foreach (var child in obj.ToArray())
            {
                if (child.Value is JsonValue value
                    && value.TryGetValue<string>(out var text)
                    && ShouldInspectResourceProperty(child.Key)
                    && TryMapLegacyResourceValue(text, resourceState, out var uri))
                {
                    obj[child.Key] = uri;
                    continue;
                }

                if (child.Value is not null)
                {
                    RewriteResourceStrings(child.Value, resourceState, child.Key);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    RewriteResourceStrings(child, resourceState, propertyName);
                }
            }
        }
    }

    private static bool TryMapLegacyResourceValue(string value, ResourceConvertState resourceState, out string uri)
    {
        uri = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("pack://application:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("bpui://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expanded = Environment.ExpandEnvironmentVariables(value).Replace('\\', '/');
        var fileName = Path.GetFileName(expanded);
        if (!string.IsNullOrWhiteSpace(fileName)
            && resourceState.ByFileName.TryGetValue(fileName, out var fileUri))
        {
            uri = fileUri;
            return true;
        }

        var normalized = expanded.TrimStart('/');
        if (resourceState.ByLegacyRelativePath.TryGetValue(normalized, out var relativeUri))
        {
            uri = relativeUri;
            return true;
        }

        return false;
    }

    private static bool ShouldInspectResourceProperty(string propertyName)
    {
        return string.Equals(propertyName, nameof(FrontedCanvasConfig.BackgroundImage), StringComparison.Ordinal)
               || propertyName.EndsWith("ImagePath", StringComparison.Ordinal)
               || propertyName.EndsWith("ImageSource", StringComparison.Ordinal)
               || propertyName.EndsWith("ResourcePath", StringComparison.Ordinal)
               || propertyName.EndsWith("LockImageSource", StringComparison.Ordinal)
               || propertyName.EndsWith("BorderImagePath", StringComparison.Ordinal);
    }

}

#pragma warning restore CS1591
