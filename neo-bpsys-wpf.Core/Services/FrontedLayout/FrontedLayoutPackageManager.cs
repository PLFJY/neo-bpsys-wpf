#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台布局包管理器，负责布局包的安装、激活、删除、复制以及包列表查询等功能。
/// 内置包（PackageId = "builtin"）为只读的默认布局方案，本地包（PackageId = "local"）为不可激活的资源包。
/// </summary>
public sealed class FrontedLayoutPackageManager : IFrontedLayoutPackageManager
{
    /// <summary>
    /// 内置布局包的 PackageId。
    /// </summary>
    public const string BuiltInPackageId = "builtin";

    /// <summary>
    /// 本地资源包的 PackageId，不可激活为布局方案。
    /// </summary>
    public const string LocalPackageId = "local";
    private const string ActivePackageFileName = "active-package.json";
    private const string ManifestFileName = "manifest.json";

    private static readonly Regex SafePackageIdRegex = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _packageRoot;
    private readonly string _builtInLayoutRoot;
    private readonly string _userLayoutRoot;
    private readonly Func<string, string>? _localize;
    private readonly ILogger<FrontedLayoutPackageManager> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    public FrontedLayoutPackageManager()
        : this(
            AppConstants.FrontedLayoutPackagesPath,
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            AppConstants.FrontedLayoutsPath,
            NullLogger<FrontedLayoutPackageManager>.Instance)
    {
    }

    /// <summary>
    /// 使用默认路径和指定的日志记录器初始化布局包管理器。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public FrontedLayoutPackageManager(ILogger<FrontedLayoutPackageManager> logger)
        : this(logger, null)
    {
    }

    /// <summary>
    /// 使用默认路径、日志记录器和本地化委托初始化布局包管理器。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="localize">本地化委托，用于翻译包名称和描述。</param>
    public FrontedLayoutPackageManager(
        ILogger<FrontedLayoutPackageManager> logger,
        Func<string, string>? localize)
        : this(
            AppConstants.FrontedLayoutPackagesPath,
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            AppConstants.FrontedLayoutsPath,
            logger,
            localize)
    {
    }

    /// <summary>
    /// 使用自定义根路径初始化布局包管理器。
    /// </summary>
    /// <param name="packageRoot">包存储根目录。</param>
    /// <param name="builtInLayoutRoot">内置布局根目录。</param>
    /// <param name="userLayoutRoot">用户布局根目录，默认使用 <see cref="AppConstants.FrontedLayoutsPath"/>。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="localize">本地化委托。</param>
    public FrontedLayoutPackageManager(
        string packageRoot,
        string builtInLayoutRoot,
        string? userLayoutRoot = null,
        ILogger<FrontedLayoutPackageManager>? logger = null,
        Func<string, string>? localize = null)
    {
        _packageRoot = packageRoot;
        _builtInLayoutRoot = builtInLayoutRoot;
        _userLayoutRoot = userLayoutRoot ?? AppConstants.FrontedLayoutsPath;
        _localize = localize;
        _logger = logger ?? NullLogger<FrontedLayoutPackageManager>.Instance;
    }

    /// <summary>
    /// 列出所有可用的布局包（包括内置包和已安装包）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>布局包信息列表。</returns>
    public async Task<IReadOnlyList<FrontedLayoutPackageInfo>> ListPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        var activeState = await GetActivePackageStateAsync(cancellationToken);
        var packages = new List<FrontedLayoutPackageInfo>
        {
            CreateBuiltInPackage(activeState.PackageId)
        };

        if (!Directory.Exists(_packageRoot))
        {
            return packages;
        }

        foreach (var directory in Directory.EnumerateDirectories(_packageRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var packageId = Path.GetFileName(directory);
            if (IsReservedPackageEntry(packageId))
            {
                continue;
            }

            packages.Add(await LoadInstalledPackageAsync(directory, packageId, activeState.PackageId, cancellationToken));
        }

        return packages
            .OrderBy(package => package.Source)
            .ThenBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 获取当前激活的布局包状态。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>激活包状态。</returns>
    public async Task<FrontedLayoutActivePackageState> GetActivePackageStateAsync(
        CancellationToken cancellationToken = default)
    {
        var path = GetActivePackageStatePath();
        if (!File.Exists(path))
        {
            return new FrontedLayoutActivePackageState
            {
                PackageId = BuiltInPackageId,
                ActivatedAt = DateTimeOffset.MinValue
            };
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var state = JsonSerializer.Deserialize<FrontedLayoutActivePackageState>(json, _jsonSerializerOptions);
            if (state is null || string.IsNullOrWhiteSpace(state.PackageId) || !IsSafePackageId(state.PackageId))
            {
                return CreateBuiltInActiveState();
            }

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read fronted layout active package state.");
            return CreateBuiltInActiveState();
        }
    }

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

    public string GetPackageLayoutsRootFolder(string packageId)
    {
        if (string.Equals(packageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            return _builtInLayoutRoot;
        }

        if (string.Equals(packageId, LocalPackageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The local resource package is not a layout scheme.");
        }

        EnsureSafePackageId(packageId);
            return Path.Combine(GetInstalledPackagePath(packageId), "FrontedLayouts");
    }

    /// <summary>
    /// 获取指定包中特定窗口的布局文件完整路径。
    /// </summary>
    /// <param name="packageId">包 ID。</param>
    /// <param name="canonicalWindowId">窗口 Canonical ID。</param>
    /// <returns>布局文件的完整路径。</returns>
    public string GetPackageLayoutPath(string packageId, string canonicalWindowId)
    {
        return Path.Combine(
            GetPackageLayoutsRootFolder(packageId),
            FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(canonicalWindowId));
    }

    public string GetPackageRootFolder()
    {
        return _packageRoot;
    }

    private FrontedLayoutPackageInfo CreateBuiltInPackage(string activePackageId)
    {
        return new FrontedLayoutPackageInfo
        {
            PackageId = BuiltInPackageId,
            Name = LocalizedOrFallback("BuiltInLayoutSchemeName", "Built-in Layout Scheme"),
            Description = LocalizedOrFallback("BuiltInLayoutSchemeDescription", "Built-in Designer v3 frontend layouts."),
            Source = FrontedLayoutPackageSource.BuiltIn,
            IsBuiltin = true,
            IsActivePackage = string.Equals(activePackageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase),
            InstallPath = _builtInLayoutRoot,
            LayoutCount = CountFiles(_builtInLayoutRoot, "*.json"),
            ResourceCount = 0,
            ValidationStatus = FrontedLayoutPackageValidationStatus.Valid
        };
    }

    private async Task<FrontedLayoutPackageInfo> LoadInstalledPackageAsync(
        string directory,
        string packageIdFromFolder,
        string activePackageId,
        CancellationToken cancellationToken)
    {
        var info = new FrontedLayoutPackageInfo
        {
            PackageId = packageIdFromFolder,
            Name = packageIdFromFolder,
            InstallPath = directory,
            Source = FrontedLayoutPackageSource.Installed,
            IsActivePackage = string.Equals(packageIdFromFolder, activePackageId, StringComparison.OrdinalIgnoreCase),
            ValidationStatus = FrontedLayoutPackageValidationStatus.Valid,
            LayoutCount = CountFiles(Path.Combine(directory, "FrontedLayouts"), "*.json"),
            ResourceCount = CountFiles(Path.Combine(directory, "Resources"), "*")
                + CountFiles(Path.Combine(directory, "resources"), "*")
        };

        if (!IsSafePackageId(packageIdFromFolder))
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
            info.ValidationMessage = "PackageId is not safe.";
            return info;
        }

        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
            info.ValidationMessage = "manifest.json is missing.";
            return info;
        }

        try
        {
            if (new FileInfo(manifestPath).Length > FrontedLayoutLimits.MaxManifestBytes)
            {
                info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
                info.ValidationMessage = "ManifestTooLarge";
                return info;
            }

            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions { MaxDepth = FrontedLayoutLimits.MaxJsonDepth });
            ApplyManifest(info, document.RootElement);
        }
        catch (Exception ex)
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
            info.ValidationMessage = ex.Message;
        }

        return info;
    }

    private static void ApplyManifest(FrontedLayoutPackageInfo info, JsonElement root)
    {
        var manifestPackageId = GetString(root, "PackageId");
        if (string.IsNullOrWhiteSpace(manifestPackageId))
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
            info.ValidationMessage = "PackageId is missing.";
            return;
        }

        if (!IsSafePackageId(manifestPackageId))
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
            info.ValidationMessage = "PackageId is not safe.";
            return;
        }

        if (!string.Equals(manifestPackageId, info.PackageId, StringComparison.OrdinalIgnoreCase))
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Warning;
            info.ValidationMessage = "PackageId does not match the install folder name.";
        }

        info.PackageId = manifestPackageId;
        info.Name = GetString(root, "Name") ?? manifestPackageId;
        info.Description = GetString(root, "Description") ?? string.Empty;
        info.Author = GetString(root, "Author") ?? string.Empty;
        info.MinVersion = GetString(root, "MinVersion") ?? string.Empty;

        var createdAt = GetString(root, "CreatedAt");
        if (DateTimeOffset.TryParse(createdAt, out var parsedCreatedAt))
        {
            info.CreatedAt = parsedCreatedAt;
        }

        if (root.TryGetProperty("Content", out var content))
        {
            if (content.TryGetProperty("Layouts", out var layouts) && layouts.ValueKind == JsonValueKind.Array)
            {
                info.LayoutCount = layouts.GetArrayLength();
            }

            if (content.TryGetProperty("Resources", out var resources) && resources.ValueKind == JsonValueKind.Array)
            {
                info.ResourceCount = resources.GetArrayLength();
            }
        }
    }

    private string GetInstalledPackagePath(string packageId)
    {
        return Path.Combine(_packageRoot, packageId);
    }

    private string GetActivePackageStatePath()
    {
        return Path.Combine(_packageRoot, ActivePackageFileName);
    }

    private async Task<(string PackageId, string DisplayName)> GenerateUserSchemeIdentityAsync(
        string? requestedName,
        CancellationToken cancellationToken)
    {
        var packages = await ListPackagesAsync(cancellationToken);
        var usedNames = packages.Select(package => package.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedIds = packages.Select(package => package.PackageId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(requestedName))
        {
            var trimmedName = requestedName.Trim();
            for (var i = 1; i < 10000; i++)
            {
                var name = i == 1 ? trimmedName : $"{trimmedName} {i}";
                var id = GenerateSafePackageIdFromName(name, i);
                if (!usedNames.Contains(name) && !usedIds.Contains(id) && !Directory.Exists(GetInstalledPackagePath(id)))
                {
                    return (id, name);
                }
            }
        }

        var format = LocalizedOrFallback("UserLayoutSchemeNameFormat", "User Layout Scheme {0}");
        for (var i = 1; i < 10000; i++)
        {
            var name = string.Format(format, i);
            var id = $"user-layout-scheme-{i}";
            if (!usedNames.Contains(name) && !usedIds.Contains(id) && !Directory.Exists(GetInstalledPackagePath(id)))
            {
                return (id, name);
            }
        }

        throw new InvalidOperationException("Failed to generate a unique layout scheme name.");
    }

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
        manifest.Format = "neo-bpsys-bpui";
        manifest.FormatVersion = 3;
        manifest.LayoutSchemaVersion = 3;
        manifest.Content ??= new FrontedLayoutPackageManifestContent();
        manifest.Content.Layouts = EnumerateLayoutEntries(packagePath).ToList();
        manifest.Content.Resources = EnumerateResourceEntries(packagePath).ToList();
        return manifest;
    }

    private async Task WriteManifestAsync(
        string packagePath,
        FrontedLayoutPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(packagePath);
        var json = JsonSerializer.Serialize(manifest, _jsonSerializerOptions);
        await File.WriteAllTextAsync(Path.Combine(packagePath, ManifestFileName), json, cancellationToken);
    }

    private static IEnumerable<FrontedLayoutPackageLayoutEntry> EnumerateLayoutEntries(string packagePath)
    {
        var layoutsRoot = Path.Combine(packagePath, "FrontedLayouts");
        if (!Directory.Exists(layoutsRoot))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(layoutsRoot, "*.json", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(layoutsRoot, file);
            // 路径反解析统一委托给 FrontedV3LayoutWindowPathHelper，PackageManager 不再自实现。
            if (!FrontedV3LayoutWindowPathHelper.TryToCanonicalWindowIdFromLayoutRelativePath(
                    relativePath,
                    out var window))
            {
                continue;
            }

            yield return new FrontedLayoutPackageLayoutEntry
            {
                Window = window,
                Path = Path.Combine("FrontedLayouts", relativePath).Replace('\\', '/')
            };
        }
    }

    private static IEnumerable<FrontedLayoutPackageResourceEntry> EnumerateResourceEntries(string packagePath)
    {
        var resourcesRoot = Directory.Exists(Path.Combine(packagePath, "Resources"))
            ? Path.Combine(packagePath, "Resources")
            : Path.Combine(packagePath, "resources");
        if (!Directory.Exists(resourcesRoot))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(resourcesRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(packagePath, file).Replace('\\', '/');
            yield return new FrontedLayoutPackageResourceEntry
            {
                Id = relativePath,
                Kind = "File",
                Path = relativePath,
                Uri = relativePath
            };
        }
    }

    private async Task CopyDirectoryContentsAsync(
        string sourceRoot,
        string targetRoot,
        CancellationToken cancellationToken,
        IReadOnlySet<string>? excludedRootFiles = null)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        var fullTargetRoot = EnsureTrailingSeparator(Path.GetFullPath(targetRoot));
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceRoot, file);
            if (excludedRootFiles is not null
                && !relativePath.Contains(Path.DirectorySeparatorChar)
                && !relativePath.Contains(Path.AltDirectorySeparatorChar)
                && excludedRootFiles.Contains(relativePath))
            {
                continue;
            }

            if (!IsSafeRelativePath(relativePath))
            {
                throw new InvalidOperationException("Package file path is not safe.");
            }

            var targetPath = Path.Combine(targetRoot, relativePath);
            var fullTargetPath = Path.GetFullPath(targetPath);
            if (!fullTargetPath.StartsWith(fullTargetRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Package copy target escaped package root.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullTargetPath)!);
            File.Copy(file, fullTargetPath, overwrite: true);
            await Task.CompletedTask;
        }
    }

    private string LocalizedOrFallback(string key, string fallback)
    {
        var localized = _localize?.Invoke(key);
        return string.IsNullOrWhiteSpace(localized) || string.Equals(localized, key, StringComparison.Ordinal)
            ? fallback
            : localized;
    }

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
               && !packageId.Contains('%', StringComparison.Ordinal);
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

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}

#pragma warning restore CS1591
