#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台布局包导入器的布局与行为校验逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageImporter
{
    private async Task<List<PackageLayoutState>> LoadPackageLayoutsAsync(
        string stagingRoot,
        FrontedLayoutPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        var layouts = new List<PackageLayoutState>();
        foreach (var layout in manifest.Content.Layouts.Concat(manifest.Content.CustomWindows))
        {
            var path = CombineInsideRoot(stagingRoot, layout.Path);
            var config = JsonSerializer.Deserialize<FrontedWindowConfig>(
                await File.ReadAllTextAsync(path, cancellationToken),
                _jsonSerializerOptions)
                ?? throw new FrontedLayoutConfigException($"Layout JSON is invalid: {layout.Path}");
            // 仅反序列化用于校验/扫描，不重写磁盘上的 Layout JSON。
            // 重写会丢失宿主暂不认识的根字段、未来版本扩展字段，并改写原始 JSON 格式与属性顺序，
            // 违反"读取期不应重写持久化内容"的仓库规则。只有显式 legacy migration 才允许生成新 JSON。
            layouts.Add(new PackageLayoutState(layout.Window, layout.Path, config));
        }

        manifest.PluginDependencies = FrontedLayoutPluginDependencyScanner.MergePackageDependencies(
            layouts.Select(layout => (layout.Window, FrontedLayoutConstants.BaseCanvasName, FrontedWindowConfigCanvasAdapter.ToCanvasConfig(layout.Config))),
            manifest.PluginDependencies,
            _controlRegistry);
        return layouts;
    }

    private async Task<List<(string Window, FrontedBehaviorDocument Document)>> LoadPackageBehaviorDocumentsAsync(
        string stagingRoot,
        FrontedLayoutPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        var documents = new List<(string Window, FrontedBehaviorDocument Document)>();
        foreach (var layout in manifest.Content.Layouts.Concat(manifest.Content.CustomWindows))
        {
            var layoutRelativePath = FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(layout.Window);
            var folder = Path.GetDirectoryName(layoutRelativePath);
            var fileName = $"{Path.GetFileNameWithoutExtension(layoutRelativePath)}.behaviors.json";
            var relativePath = string.IsNullOrWhiteSpace(folder)
                ? Path.Combine("FrontedBehaviors", fileName)
                : Path.Combine("FrontedBehaviors", folder, fileName);
            var path = CombineInsideRoot(stagingRoot, relativePath);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var document = JsonSerializer.Deserialize<FrontedBehaviorDocument>(
                    await File.ReadAllTextAsync(path, cancellationToken),
                    _jsonSerializerOptions);
                if (document is not null)
                {
                    documents.Add((layout.Window, document));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to scan behavior plugin dependencies for {Window}; the behavior file remains preserved.",
                    layout.Window);
            }
        }

        return documents;
    }

    private async Task<FrontedLayoutPackageImportResult> ValidatePackageAsync(
        string stagingRoot,
        FrontedLayoutPackageManifest? manifest,
        bool compressOversizedImages,
        CancellationToken cancellationToken)
    {
        if (manifest is null)
        {
            return Fail("Invalid package manifest.");
        }

        if (!string.Equals(manifest.Format, "neo-bpsys-bpui", StringComparison.Ordinal)
            || manifest.FormatVersion != 3)
        {
            return manifest.FormatVersion > 3
                ? new FrontedLayoutPackageImportResult { Success = false, RequiresNewerApp = true, ErrorMessage = "Package requires a newer app version." }
                : Fail("Invalid package manifest.");
        }

        if (!string.Equals(
                manifest.LayoutModel,
                FrontedLayoutConstants.WindowCentricLayoutModel,
                StringComparison.Ordinal))
        {
            return Fail("Package layout model is not window-centric.");
        }

        if (!FrontedLayoutPackageManager.IsSafePackageId(manifest.PackageId)
            || string.Equals(manifest.PackageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(manifest.PackageId, FrontedLayoutPackageManager.LocalPackageId, StringComparison.OrdinalIgnoreCase))
        {
            return Fail("PackageId is invalid.");
        }

        var manifestLimitError = ValidateManifestTextLengths(manifest);
        if (!string.IsNullOrWhiteSpace(manifestLimitError))
        {
            return Fail(manifestLimitError);
        }

        if (manifest.LayoutSchemaVersion != 3)
        {
            return Fail("Layout schema version is not supported.");
        }

        if (RequiresNewerApp(manifest.MinVersion))
        {
            return new FrontedLayoutPackageImportResult
            {
                Success = false,
                RequiresNewerApp = true,
                PackageId = manifest.PackageId,
                ErrorMessage = "Package requires a newer app version."
            };
        }

        if (File.Exists(Path.Combine(stagingRoot, "Config.json"))
            || Directory.Exists(Path.Combine(stagingRoot, "CustomUi"))
            || Directory.Exists(Path.Combine(stagingRoot, "FrontElementsConfig")))
        {
            return Fail("v3 packages must not contain legacy Config.json, CustomUi, or FrontElementsConfig content.");
        }

        manifest.Content ??= new FrontedLayoutPackageManifestContent();
        manifest.Content.Layouts ??= [];
        manifest.Content.CustomWindows ??= [];
        manifest.Content.Resources ??= [];
        var allLayouts = manifest.Content.Layouts
            .Select(layout => (Layout: layout, IsCustom: false))
            .Concat(manifest.Content.CustomWindows.Select(layout => (Layout: layout, IsCustom: true)))
            .ToArray();
        if (allLayouts.Length == 0)
        {
            return Fail("Package contains no layouts.");
        }

        if (allLayouts.Length > FrontedLayoutLimits.MaxLayoutsPerPackage)
        {
            return Fail("TooManyLayouts");
        }

        if (manifest.Content.Resources.Count > FrontedLayoutLimits.MaxResourcesPerPackage)
        {
            return Fail("TooManyResources");
        }

        var windows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var layoutPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layoutState in allLayouts)
        {
            var layout = layoutState.Layout;
            if (!windows.Add(layout.Window) || !layoutPaths.Add(layout.Path))
            {
                return Fail("Package contains duplicate layout identities or paths.");
            }

            if (!IsSafeRelativePath(layout.Path)
                || !FrontedV3LayoutWindowPathHelper.IsSafeCanonicalWindowId(layout.Window))
            {
                return Fail("Layout path is not safe.");
            }

            var isCustomWindow = FrontedV3LayoutWindowPathHelper.TryParseCustomCanonicalWindowId(
                layout.Window, out var customPackageId, out _);
            if (isCustomWindow != layoutState.IsCustom
                || (isCustomWindow
                    && !string.Equals(customPackageId, manifest.PackageId, StringComparison.OrdinalIgnoreCase)))
            {
                return Fail("Custom window package identity is invalid.");
            }

            if (!TryGetExpectedWindowFromPath(layout.Path, out var expectedWindow)
                || !string.Equals(expectedWindow, layout.Window, StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"Layout Window '{layout.Window}' does not match path '{layout.Path}'.");
            }

            var layoutPath = CombineInsideRoot(stagingRoot, layout.Path);
            if (!File.Exists(layoutPath))
            {
                return Fail($"Layout file is missing: {layout.Path}");
            }

            if (new FileInfo(layoutPath).Length > FrontedLayoutLimits.MaxLayoutJsonBytes)
            {
                return Fail("LayoutJsonTooLarge");
            }

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(
                    await File.ReadAllTextAsync(layoutPath, cancellationToken),
                    nodeOptions: null,
                    documentOptions: new JsonDocumentOptions { MaxDepth = FrontedLayoutLimits.MaxJsonDepth });
            }
            catch (Exception ex)
            {
                return Fail($"Layout JSON is invalid: {layout.Path}; {ex.Message}");
            }

            if (node is not JsonObject obj
                || !obj.TryGetPropertyValue("Version", out var versionNode)
                || !TryGetInt(versionNode, out var version)
                || version != 3)
            {
                return Fail($"Layout Version must be 3: {layout.Path}");
            }

            var resourceError = ValidateLayoutResourceReferences(obj, stagingRoot, manifest.PackageId);
            if (!string.IsNullOrWhiteSpace(resourceError))
            {
                return Fail(resourceError);
            }

            try
            {
                var config = JsonSerializer.Deserialize<FrontedWindowConfig>(
                    await File.ReadAllTextAsync(layoutPath, cancellationToken),
                    _jsonSerializerOptions);
                if (config is null)
                {
                    return Fail($"Layout JSON is invalid: {layout.Path}");
                }

                if (config.DisplayNames.Any(pair =>
                        pair.Key is not ("zh_Hans" or "en_US" or "ja_JP")
                        || (!string.IsNullOrWhiteSpace(pair.Value)
                            && pair.Value.Length > FrontedLayoutLimits.MaxWindowDisplayNameLength)))
                {
                    return Fail("InputTooLong: DisplayNames");
                }

                if (layoutState.IsCustom
                    && !config.DisplayNames.Any(pair =>
                        pair.Key is "zh_Hans" or "en_US" or "ja_JP"
                        && !string.IsNullOrWhiteSpace(pair.Value)))
                {
                    return Fail("Custom window must define at least one display name.");
                }

                var validationMessages = _validator.Validate(
                    layout.Window,
                    FrontedLayoutConstants.BaseCanvasName,
                    FrontedWindowConfigCanvasAdapter.ToCanvasConfig(config));
                var error = validationMessages.FirstOrDefault(message =>
                    message.Severity == global::neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.FrontedLayoutValidationSeverity.Error);
                if (error is not null)
                {
                    return Fail($"Layout validation failed: {layout.Path}; {error.Message}");
                }

                if (layoutState.IsCustom)
                {
                    var behaviorError = await ValidateCustomBehaviorAsync(
                        stagingRoot,
                        layout.Window,
                        cancellationToken);
                    if (!string.IsNullOrWhiteSpace(behaviorError))
                    {
                        return Fail(behaviorError);
                    }
                }
            }
            catch (Exception ex)
            {
                return Fail($"Layout JSON is invalid: {layout.Path}; {ex.Message}");
            }
        }

        var oversizedImages = new List<FrontedLayoutPackageImageIssue>();
        foreach (var resource in manifest.Content.Resources)
        {
            if (!IsSafeRelativePath(resource.Path))
            {
                return Fail("Resource path is not safe.");
            }

            var resourcePath = CombineInsideRoot(stagingRoot, resource.Path);
            if (!File.Exists(resourcePath))
            {
                return Fail($"Missing package resource: {resource.Path}");
            }

            if (IsImageResource(resource.Path))
            {
                var validation = _imageSafetyService.ValidateFile(
                    resourcePath,
                    FrontedImagePurpose.PackageResource,
                    knownBackgroundImage: false,
                    knownUiImage: false);
                if (!validation.IsValid)
                {
                    if (validation.ErrorCode is "ImageTooLarge" or "ImageTooManyPixels")
                    {
                        oversizedImages.Add(new FrontedLayoutPackageImageIssue
                        {
                            ResourcePath = resource.Path,
                            ErrorCode = validation.ErrorCode,
                            FileBytes = validation.FileBytes,
                            PixelWidth = validation.PixelWidth,
                            PixelHeight = validation.PixelHeight
                        });
                        continue;
                    }

                    return Fail(validation.ErrorCode ?? "InvalidImageResource");
                }
            }
            else if (IsFontResource(resource.Path)
                     && !FrontedFontResourceHelper.IsSupportedFontExtension(Path.GetExtension(resource.Path)))
            {
                return Fail("InvalidFontResource");
            }
        }

        if (oversizedImages.Count == 0)
        {
            var warning = GetCreatedVersionWarning(manifest.CreatedVersion);
            return new FrontedLayoutPackageImportResult
            {
                Success = true,
                CreatedVersion = manifest.CreatedVersion,
                HasCreatedVersionWarning = warning is not null,
                WarningMessage = warning
            };
        }

        if (!compressOversizedImages)
        {
            return new FrontedLayoutPackageImportResult
            {
                Success = false,
                ErrorMessage = "OversizedPackageImages",
                OversizedImages = oversizedImages
            };
        }

        var compressedImages = await Task.Run(
            () => CompressOversizedImages(stagingRoot, manifest.Content.Resources, oversizedImages, cancellationToken),
            cancellationToken);
        if (compressedImages is null)
        {
            return new FrontedLayoutPackageImportResult
            {
                Success = false,
                ErrorMessage = "PackageImageCompressionFailed",
                OversizedImages = oversizedImages
            };
        }

        foreach (var issue in oversizedImages)
        {
            var resourcePath = CombineInsideRoot(stagingRoot, issue.ResourcePath);
            var validation = _imageSafetyService.ValidateFile(
                resourcePath,
                FrontedImagePurpose.PackageResource,
                knownBackgroundImage: false,
                knownUiImage: false);
            if (!validation.IsValid)
            {
                return new FrontedLayoutPackageImportResult
                {
                    Success = false,
                    ErrorMessage = validation.ErrorCode ?? "PackageImageCompressionFailed",
                    OversizedImages = [issue]
                };
            }
        }

        await File.WriteAllTextAsync(
            Path.Combine(stagingRoot, ManifestFileName),
            JsonSerializer.Serialize(manifest, _jsonSerializerOptions),
            cancellationToken);

        var createdVersionWarning = GetCreatedVersionWarning(manifest.CreatedVersion);
        return new FrontedLayoutPackageImportResult
        {
            Success = true,
            CompressedImages = compressedImages,
            CreatedVersion = manifest.CreatedVersion,
            HasCreatedVersionWarning = createdVersionWarning is not null,
            WarningMessage = createdVersionWarning
        };
    }

    private static string? GetCreatedVersionWarning(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!FrontedPackageVersionComparer.TryParseNumeric(value, out var createdVersion)
            || !FrontedPackageVersionComparer.TryParseNumeric(AppConstants.AppVersion, out var currentVersion))
        {
            return "PackageCreatedVersionInvalid";
        }

        return currentVersion < createdVersion
            ? "PackageCreatedByNewerVersion"
            : null;
    }

    private async Task<string?> ValidateCustomBehaviorAsync(
        string stagingRoot,
        string canonicalWindowId,
        CancellationToken cancellationToken)
    {
        var layoutRelativePath = FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(canonicalWindowId);
        var layoutFolder = Path.GetDirectoryName(layoutRelativePath);
        var behaviorFileName = $"{Path.GetFileNameWithoutExtension(layoutRelativePath)}.behaviors.json";
        var behaviorRelativePath = string.IsNullOrWhiteSpace(layoutFolder)
            ? Path.Combine("FrontedBehaviors", behaviorFileName)
            : Path.Combine("FrontedBehaviors", layoutFolder, behaviorFileName);
        var behaviorPath = CombineInsideRoot(stagingRoot, behaviorRelativePath);
        if (!File.Exists(behaviorPath))
        {
            return null;
        }

        if (new FileInfo(behaviorPath).Length > FrontedLayoutLimits.MaxLayoutJsonBytes)
        {
            return "BehaviorJsonTooLarge";
        }

        try
        {
            var document = JsonSerializer.Deserialize<FrontedBehaviorDocument>(
                await File.ReadAllTextAsync(behaviorPath, cancellationToken),
                _jsonSerializerOptions);
            if (document is null
                || document.Version != 1
                || !string.Equals(document.WindowType, canonicalWindowId, StringComparison.Ordinal)
                || !string.Equals(
                    document.CanvasName,
                    FrontedLayoutConstants.BaseCanvasName,
                    StringComparison.Ordinal))
            {
                return $"Custom behavior identity is invalid: {behaviorRelativePath.Replace('\\', '/')}";
            }
        }
        catch (Exception ex)
        {
            return $"Behavior JSON is invalid: {behaviorRelativePath.Replace('\\', '/')}; {ex.Message}";
        }

        return null;
    }
}

#pragma warning restore CS1591
