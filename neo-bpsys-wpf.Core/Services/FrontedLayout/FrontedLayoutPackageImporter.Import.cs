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
/// 前台布局包导入器的导入编排逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageImporter
{
    /// <summary>
    /// 从 .bpui 压缩包文件导入布局包。
    /// </summary>
    /// <param name="request">导入请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导入结果。</returns>
    public async Task<FrontedLayoutPackageImportResult> ImportAsync(
        FrontedLayoutPackageImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var stagingRoot = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        try
        {
            if (string.IsNullOrWhiteSpace(request.PackagePath) || !File.Exists(request.PackagePath))
            {
                return Fail("Package archive was not found.");
            }

            if (new FileInfo(request.PackagePath).Length > FrontedLayoutLimits.MaxPackageArchiveBytes)
            {
                return Fail("PackageTooLarge");
            }

            var oversizedArchiveImages = FindOversizedArchiveImages(request.PackagePath);
            if (oversizedArchiveImages.Count > 0 && !request.CompressOversizedImages)
            {
                return new FrontedLayoutPackageImportResult
                {
                    Success = false,
                    ErrorMessage = "OversizedPackageImages",
                    OversizedImages = oversizedArchiveImages
                };
            }

            Directory.CreateDirectory(stagingRoot);
            ExtractZipSafely(request.PackagePath, stagingRoot, request.CompressOversizedImages);
            var result = await ImportStagedDirectoryAsync(
                stagingRoot,
                request.ReplaceExisting,
                request.ActivateAfterImport,
                request.CompressOversizedImages,
                cancellationToken);
            if (result.Success)
            {
                stagingRoot = string.Empty;
            }

            return result;
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Invalid bpui archive.");
            return Fail($"Invalid package archive: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to import fronted layout package.");
            return Fail(ex.Message);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    /// <summary>
    /// 从本地目录导入布局包。
    /// </summary>
    /// <param name="packageDirectory">包目录路径。</param>
    /// <param name="replaceExisting">是否替换已存在的同名包。</param>
    /// <param name="activateAfterImport">导入后是否立即激活。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导入结果。</returns>
    public async Task<FrontedLayoutPackageImportResult> ImportDirectoryAsync(
        string packageDirectory,
        bool replaceExisting,
        bool activateAfterImport,
        CancellationToken cancellationToken = default)
    {
        var stagingRoot = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        try
        {
            if (string.IsNullOrWhiteSpace(packageDirectory) || !Directory.Exists(packageDirectory))
            {
                return Fail("Package directory was not found.");
            }

            CopyDirectory(packageDirectory, stagingRoot);
            var result = await ImportStagedDirectoryAsync(
                stagingRoot,
                replaceExisting,
                activateAfterImport,
                compressOversizedImages: false,
                cancellationToken: cancellationToken);
            if (result.Success)
            {
                stagingRoot = string.Empty;
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to import fronted layout package directory.");
            return Fail(ex.Message);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    private async Task<FrontedLayoutPackageImportResult> ImportStagedDirectoryAsync(
        string stagingRoot,
        bool replaceExisting,
        bool activateAfterImport,
        bool compressOversizedImages,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(stagingRoot, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return DetectLegacyPackage(stagingRoot)
                ? Legacy()
                : Fail("manifest.json is missing.");
        }

        FrontedLayoutPackageManifest? manifest;
        try
        {
            if (new FileInfo(manifestPath).Length > FrontedLayoutLimits.MaxManifestBytes)
            {
                return Fail("ManifestTooLarge");
            }

            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            manifest = JsonSerializer.Deserialize<FrontedLayoutPackageManifest>(json, _jsonSerializerOptions);
        }
        catch (Exception ex)
        {
            return DetectLegacyPackage(stagingRoot)
                ? Legacy()
                : Fail($"Invalid package manifest: {ex.Message}");
        }

        var validation = await ValidatePackageAsync(
            stagingRoot,
            manifest,
            compressOversizedImages,
            cancellationToken);
        if (!validation.Success)
        {
            return validation;
        }

        var validatedManifest = manifest!;
        var packageLayouts = await LoadPackageLayoutsAsync(stagingRoot, validatedManifest, cancellationToken);
        var behaviorDocuments = await LoadPackageBehaviorDocumentsAsync(
            stagingRoot,
            validatedManifest,
            cancellationToken);
        validatedManifest.PluginDependencies = FrontedLayoutPluginDependencyScanner.MergePackageDependencies(
            packageLayouts.Select(layout => (layout.Window, FrontedLayoutConstants.BaseCanvasName, FrontedWindowConfigCanvasAdapter.ToCanvasConfig(layout.Config))),
            validatedManifest.PluginDependencies,
            _controlRegistry,
            _pluginMetadataProvider,
            behaviorDocuments);
        var missingPluginControls = FrontedLayoutPluginDependencyScanner.FindMissingPluginControls(
            packageLayouts.Select(layout => (layout.Window, FrontedLayoutConstants.BaseCanvasName, FrontedWindowConfigCanvasAdapter.ToCanvasConfig(layout.Config))),
            _controlRegistry);
        var unsatisfiedPluginDependencies = FrontedLayoutPluginDependencyScanner.FindUnsatisfiedPluginDependencies(
            packageLayouts.Select(layout => (layout.Window, FrontedLayoutConstants.BaseCanvasName, FrontedWindowConfigCanvasAdapter.ToCanvasConfig(layout.Config))),
            validatedManifest.PluginDependencies,
            _controlRegistry,
            _pluginMetadataProvider,
            behaviorDocuments,
            _behaviorEventCatalog);
        var packageId = validatedManifest.PackageId;
        var installPath = GetInstalledPackagePath(packageId);
        if (Directory.Exists(installPath) && !replaceExisting)
        {
            return new FrontedLayoutPackageImportResult
            {
                Success = false,
                PackageId = packageId,
                PackageAlreadyExists = true,
                ErrorMessage = "Package already exists."
            };
        }

        Directory.CreateDirectory(_packageRoot);
        var packageRoot = EnsureTrailingSeparator(Path.GetFullPath(_packageRoot));
        var fullInstallPath = Path.GetFullPath(installPath);
        if (!fullInstallPath.StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Fail("Package install path escaped the package root.");
        }

        if (Directory.Exists(installPath))
        {
            Directory.Delete(installPath, recursive: true);
        }

        Directory.Move(stagingRoot, installPath);
        if (activateAfterImport && _packageManager is not null)
        {
            await _packageManager.ActivatePackageAsync(packageId, cancellationToken);
        }

        return new FrontedLayoutPackageImportResult
        {
            Success = true,
            PackageId = packageId,
            InstalledPath = installPath,
            LayoutCount = validatedManifest.Content.Layouts.Count + validatedManifest.Content.CustomWindows.Count,
            ResourceCount = validatedManifest.Content.Resources.Count,
            MissingPluginControls = missingPluginControls,
            UnsatisfiedPluginDependencies = unsatisfiedPluginDependencies,
            CompressedImages = validation.CompressedImages,
            HasCreatedVersionWarning = validation.HasCreatedVersionWarning,
            CreatedVersion = validation.CreatedVersion,
            WarningMessage = validation.WarningMessage
        };
    }
}

#pragma warning restore CS1591
