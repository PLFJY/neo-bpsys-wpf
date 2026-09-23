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
/// 前台布局包管理器的Crud逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageManager
{
    /// <summary>
    /// 复制指定包为新的布局方案。内置包会从内置资源复制，已安装包从源目录复制。
    /// 复制完成后自动激活新包。
    /// </summary>
    /// <param name="sourcePackageId">源包 ID。</param>
    /// <param name="requestedName">请求的新方案名称，为 null 时自动生成。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>复制后的新包信息。</returns>
    public async Task<FrontedLayoutPackageInfo> DuplicatePackageAsync(
        string sourcePackageId,
        string? requestedName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(sourcePackageId, LocalPackageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The local resource package cannot be duplicated as a layout scheme.");
        }

        var (packageId, displayName) = await GenerateUserSchemeIdentityAsync(requestedName, cancellationToken);
        var targetPath = GetInstalledPackagePath(packageId);
        Directory.CreateDirectory(_packageRoot);

        if (string.Equals(sourcePackageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            await CopyDirectoryContentsAsync(
                _builtInLayoutRoot,
                Path.Combine(targetPath, "FrontedLayouts"),
                cancellationToken);
            var builtInResourcesRoot = Path.GetDirectoryName(Path.GetFullPath(_builtInLayoutRoot));
            if (builtInResourcesRoot is not null)
            {
                await CopyDirectoryContentsAsync(
                    Path.Combine(builtInResourcesRoot, "FrontedBehaviors"),
                    Path.Combine(targetPath, "FrontedBehaviors"),
                    cancellationToken);
            }
        }
        else
        {
            EnsureSafePackageId(sourcePackageId);
            var sourcePath = GetInstalledPackagePath(sourcePackageId);
            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException(sourcePath);
            }

            var sourceManifestPath = Path.Combine(sourcePath, ManifestFileName);
            if (!File.Exists(sourceManifestPath))
            {
                throw new FileNotFoundException("Package manifest is missing.", sourceManifestPath);
            }

            await CopyDirectoryContentsAsync(
                sourcePath,
                targetPath,
                cancellationToken,
                excludedRootFiles: new HashSet<string>([ActivePackageFileName], StringComparer.OrdinalIgnoreCase));
        }

        var manifest = await CreateDuplicateManifestAsync(
            targetPath,
            packageId,
            displayName,
            sourcePackageId,
            cancellationToken);
        await WriteManifestAsync(targetPath, manifest, cancellationToken);
        await ActivatePackageAsync(packageId, cancellationToken);

        return await LoadInstalledPackageAsync(targetPath, packageId, packageId, cancellationToken);
    }

    /// <summary>
    /// 更新已安装布局包的显示名称，不改变包 ID、目录或包内资源 URI。
    /// </summary>
    /// <param name="packageId">要改名的包 ID。</param>
    /// <param name="name">新的显示名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>改名后的布局包信息。</returns>
    /// <exception cref="ArgumentException"><paramref name="packageId"/> 不安全或 <paramref name="name"/> 为空白时抛出。</exception>
    /// <exception cref="InvalidOperationException">尝试改名内置包或本地资源包时抛出。</exception>
    /// <exception cref="DirectoryNotFoundException">包目录不存在时抛出。</exception>
    /// <exception cref="FileNotFoundException">包清单文件缺失时抛出。</exception>
    public async Task<FrontedLayoutPackageInfo> RenamePackageAsync(
        string packageId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ArgumentException("Package name is required.", nameof(name));
        }

        return await UpdatePackageManifestTextAsync(packageId, "Name", trimmedName, cancellationToken);
    }

    /// <summary>
    /// 更新已安装布局包的描述，不改变包 ID、目录或包内资源 URI。
    /// </summary>
    /// <param name="packageId">要更新描述的包 ID。</param>
    /// <param name="description">新的包描述，可以为空。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>更新后的布局包信息。</returns>
    /// <exception cref="ArgumentException"><paramref name="packageId"/> 不安全时抛出。</exception>
    /// <exception cref="InvalidOperationException">尝试更新内置包或本地资源包时抛出。</exception>
    /// <exception cref="DirectoryNotFoundException">包目录不存在时抛出。</exception>
    /// <exception cref="FileNotFoundException">包清单文件缺失时抛出。</exception>
    public Task<FrontedLayoutPackageInfo> UpdatePackageDescriptionAsync(
        string packageId,
        string description,
        CancellationToken cancellationToken = default)
    {
        return UpdatePackageManifestTextAsync(packageId, "Description", description.Trim(), cancellationToken);
    }

    private async Task<FrontedLayoutPackageInfo> UpdatePackageManifestTextAsync(
        string packageId,
        string propertyName,
        string value,
        CancellationToken cancellationToken)
    {
        if (string.Equals(packageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The built-in package cannot be updated.");
        }

        if (string.Equals(packageId, LocalPackageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The local resource package cannot be updated.");
        }

        EnsureSafePackageId(packageId);
        var packagePath = GetInstalledPackagePath(packageId);
        if (!Directory.Exists(packagePath))
        {
            throw new DirectoryNotFoundException(packagePath);
        }

        var manifestPath = Path.Combine(packagePath, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Package manifest is missing.", manifestPath);
        }

        var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        if (manifestJson.Length > FrontedLayoutLimits.MaxManifestBytes)
        {
            throw new InvalidDataException("Package manifest is too large.");
        }

        var manifest = JsonNode.Parse(
            manifestJson,
            documentOptions: new JsonDocumentOptions { MaxDepth = FrontedLayoutLimits.MaxJsonDepth }) as JsonObject;
        if (manifest is null)
        {
            throw new InvalidDataException("Package manifest must be a JSON object.");
        }

        manifest[propertyName] = value;
        await File.WriteAllTextAsync(
            manifestPath,
            manifest.ToJsonString(_jsonSerializerOptions),
            cancellationToken);

        var activeState = await GetActivePackageStateAsync(cancellationToken);
        return await LoadInstalledPackageAsync(packagePath, packageId, activeState.PackageId, cancellationToken);
    }

}
