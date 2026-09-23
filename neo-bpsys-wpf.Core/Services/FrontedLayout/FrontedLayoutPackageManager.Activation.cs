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
/// 前台布局包管理器的Activation逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageManager
{
    /// <summary>
    /// 激活指定的布局包。激活内置包时删除激活状态文件恢复默认；本地包不可激活。
    /// </summary>
    /// <param name="packageId">要激活的包 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="InvalidOperationException">本地包不可激活。</exception>
    /// <exception cref="DirectoryNotFoundException">包目录不存在。</exception>
    /// <exception cref="FileNotFoundException">包清单文件缺失。</exception>
    public async Task ActivatePackageAsync(string packageId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(packageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            var statePath = GetActivePackageStatePath();
            if (File.Exists(statePath))
            {
                File.Delete(statePath);
            }

            return;
        }

        if (string.Equals(packageId, LocalPackageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The local resource package cannot be activated.");
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

        Directory.CreateDirectory(_packageRoot);
        var state = new FrontedLayoutActivePackageState
        {
            PackageId = packageId,
            ActivatedAt = DateTimeOffset.UtcNow
        };
        var json = JsonSerializer.Serialize(state, _jsonSerializerOptions);
        await File.WriteAllTextAsync(GetActivePackageStatePath(), json, cancellationToken);
    }

    public async Task DeletePackageAsync(string packageId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(packageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The built-in package cannot be deleted.");
        }

        if (string.Equals(packageId, LocalPackageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The local resource package cannot be deleted.");
        }

        EnsureSafePackageId(packageId);
        var packagePath = GetInstalledPackagePath(packageId);
        if (!Directory.Exists(packagePath))
        {
            return;
        }

        var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(_packageRoot));
        var fullPackagePath = Path.GetFullPath(packagePath);
        if (!fullPackagePath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Package directory escaped the package root.");
        }

        var activeState = await GetActivePackageStateAsync(cancellationToken);
        if (string.Equals(activeState.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
        {
            await ActivatePackageAsync(BuiltInPackageId, cancellationToken);
        }

        Directory.Delete(fullPackagePath, recursive: true);
    }

    public async Task<FrontedLayoutPackageInfo> EnsureWritableActivePackageAsync(
        CancellationToken cancellationToken = default)
    {
        var activeState = await GetActivePackageStateAsync(cancellationToken);
        if (string.Equals(activeState.PackageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            return await DuplicatePackageAsync(BuiltInPackageId, null, cancellationToken);
        }

        if (string.Equals(activeState.PackageId, LocalPackageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The local resource package cannot be used as a layout scheme.");
        }

        EnsureSafePackageId(activeState.PackageId);
        var packagePath = GetInstalledPackagePath(activeState.PackageId);
        if (!Directory.Exists(packagePath))
        {
            throw new DirectoryNotFoundException(packagePath);
        }

        var manifestPath = Path.Combine(packagePath, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Package manifest is missing.", manifestPath);
        }

        return await LoadInstalledPackageAsync(
            packagePath,
            activeState.PackageId,
            activeState.PackageId,
            cancellationToken);
    }

}
