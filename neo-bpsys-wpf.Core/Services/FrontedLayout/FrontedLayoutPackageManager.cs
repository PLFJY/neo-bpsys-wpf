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
/// 前台布局包管理器，负责布局包的安装、激活、删除、复制以及包列表查询等功能。
/// 内置包（PackageId = "builtin"）为只读的默认布局方案，本地包（PackageId = "local"）为不可激活的资源包。
/// </summary>
public sealed partial class FrontedLayoutPackageManager : IFrontedLayoutPackageManager
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

}

#pragma warning restore CS1591
