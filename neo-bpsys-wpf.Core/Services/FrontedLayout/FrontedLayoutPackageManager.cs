#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class FrontedLayoutPackageManager : IFrontedLayoutPackageManager
{
    public const string BuiltInPackageId = "builtin";
    public const string LocalPackageId = "local";
    private const string ActivePackageFileName = "active-package.json";
    private const string ManifestFileName = "manifest.json";

    private static readonly Regex SafePackageIdRegex = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _packageRoot;
    private readonly string _builtInLayoutRoot;
    private readonly ILogger<FrontedLayoutPackageManager> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public FrontedLayoutPackageManager()
        : this(
            AppConstants.FrontedLayoutPackagesPath,
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            NullLogger<FrontedLayoutPackageManager>.Instance)
    {
    }

    public FrontedLayoutPackageManager(ILogger<FrontedLayoutPackageManager> logger)
        : this(
            AppConstants.FrontedLayoutPackagesPath,
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            logger)
    {
    }

    public FrontedLayoutPackageManager(
        string packageRoot,
        string builtInLayoutRoot,
        ILogger<FrontedLayoutPackageManager>? logger = null)
    {
        _packageRoot = packageRoot;
        _builtInLayoutRoot = builtInLayoutRoot;
        _logger = logger ?? NullLogger<FrontedLayoutPackageManager>.Instance;
    }

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
            var json = await File.ReadAllTextAsync(path, cancellationToken);
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

        Directory.CreateDirectory(_packageRoot);
        var state = new FrontedLayoutActivePackageState
        {
            PackageId = packageId,
            ActivatedAt = DateTimeOffset.UtcNow
        };
        var json = JsonSerializer.Serialize(state, _jsonSerializerOptions);
        await File.WriteAllTextAsync(GetActivePackageStatePath(), json, cancellationToken);
    }

    public Task DeletePackageAsync(string packageId, CancellationToken cancellationToken = default)
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
            return Task.CompletedTask;
        }

        var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(_packageRoot));
        var fullPackagePath = Path.GetFullPath(packagePath);
        if (!fullPackagePath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Package directory escaped the package root.");
        }

        Directory.Delete(fullPackagePath, recursive: true);
        return Task.CompletedTask;
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
            Name = "System Built-in",
            Description = "Built-in Designer v3 frontend layouts.",
            Source = FrontedLayoutPackageSource.BuiltIn,
            IsBuiltin = true,
            IsActive = string.Equals(activePackageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase),
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
            IsActive = string.Equals(packageIdFromFolder, activePackageId, StringComparison.OrdinalIgnoreCase),
            ValidationStatus = FrontedLayoutPackageValidationStatus.Valid,
            LayoutCount = CountFiles(Path.Combine(directory, "layouts"), "*.json"),
            ResourceCount = CountFiles(Path.Combine(directory, "resources"), "*")
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
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            using var document = JsonDocument.Parse(json);
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
