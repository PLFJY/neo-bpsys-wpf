using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// SmartBp 识别区域配置服务实现（当前场景：GameData）。
/// 负责配置读写、导入导出、重置、校验与比例信息计算。
/// </summary>
public sealed class SmartBpRegionConfigService : ISmartBpRegionConfigService
{
    // 业务约定：当前仅管理 GameData 一份配置文件。
    private const string ConfigFileName = "GameDataRegions.json";
    private const double AspectSnapTolerance = 0.08;
    private const int AspectApproxMaxDenominator = 36;
    private const double AspectApproxRelativeTolerance = 0.015;
    private static readonly (int W, int H)[] CommonAspects =
    [
        (16, 9),
        (16, 10),
        (4, 3),
        (3, 2),
        (21, 9),
        (5, 4),
        (1, 1)
    ];

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private readonly ISmartBpSceneDefinition _gameDataSceneDefinition;

    private readonly ILogger<SmartBpRegionConfigService> _logger;
    private SmartBpRegionProfile _cachedProfile;

    /// <summary>
    /// 初始化配置服务并加载当前配置（失败时自动回退默认配置）。
    /// </summary>
    public SmartBpRegionConfigService(
        IEnumerable<ISmartBpSceneDefinition> sceneDefinitions,
        ILogger<SmartBpRegionConfigService> logger)
    {
        _logger = logger;
        _gameDataSceneDefinition = sceneDefinitions.FirstOrDefault(s =>
            string.Equals(s.SceneKey, SmartBpSceneKeys.GameData, StringComparison.OrdinalIgnoreCase));

        if (_gameDataSceneDefinition == null)
        {
            _logger.LogError("Missing SmartBp scene definition: GameData");
            throw new InvalidOperationException("Missing SmartBp scene definition: GameData");
        }

        // 先放一份内存默认值，确保任意异常下都有可用配置。
        _cachedProfile = _gameDataSceneDefinition.CreateDefaultProfile();
        _ = EnsureLoaded();
    }

    /// <summary>
    /// SmartBp 配置目录路径。
    /// </summary>
    public string ConfigDirectoryPath => Path.Combine(AppConstants.AppDataPath, "SmartBp");

    /// <summary>
    /// GameData 配置文件完整路径。
    /// </summary>
    public string GameDataConfigPath => Path.Combine(ConfigDirectoryPath, ConfigFileName);

    /// <summary>
    /// 配置被保存、导入或重置后触发。
    /// </summary>
    public event EventHandler? GameDataProfileChanged;

    /// <inheritdoc />
    public SmartBpRegionProfile GetCurrentGameDataProfile()
    {
        // 返回深拷贝，避免调用方直接修改内部缓存造成“未校验即生效”。
        _ = EnsureLoaded();
        return DeepClone(_cachedProfile);
    }

    /// <inheritdoc />
    public bool TrySaveGameDataProfile(SmartBpRegionProfile profile, out string errorMessage)
    {
        // 保存前统一走校验，避免写入坏数据。
        if (!_gameDataSceneDefinition.TryValidateProfile(profile, out errorMessage))
            return false;

        try
        {
            Directory.CreateDirectory(ConfigDirectoryPath);
            var json = JsonSerializer.Serialize(profile, _jsonOptions);
            File.WriteAllText(GameDataConfigPath, json);
            _cachedProfile = DeepClone(profile);
            // 通过事件通知 UI 刷新比例状态、路径显示等。
            GameDataProfileChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = string.Format(I18nHelper.GetLocalizedString("SmartBpRegionConfigSaveFailedFormat"), ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryImportGameDataProfile(string sourcePath, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            errorMessage = I18nHelper.GetLocalizedString("SmartBpRegionConfigImportFileNotFound");
            return false;
        }

        try
        {
            var json = File.ReadAllText(sourcePath);
            var profile = JsonSerializer.Deserialize<SmartBpRegionProfile>(json, _jsonOptions);
            if (profile == null)
            {
                errorMessage = I18nHelper.GetLocalizedString("SmartBpRegionConfigImportEmpty");
                return false;
            }

            // 导入成功后直接复用保存流程（含校验与事件通知）。
            return TrySaveGameDataProfile(profile, out errorMessage);
        }
        catch (Exception ex)
        {
            errorMessage = string.Format(I18nHelper.GetLocalizedString("SmartBpRegionConfigImportFailedFormat"), ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryExportGameDataProfile(string targetPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            errorMessage = I18nHelper.GetLocalizedString("SmartBpRegionConfigExportPathEmpty");
            return false;
        }

        try
        {
            _ = EnsureLoaded();
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_cachedProfile, _jsonOptions);
            File.WriteAllText(targetPath, json);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = string.Format(I18nHelper.GetLocalizedString("SmartBpRegionConfigExportFailedFormat"), ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryResetGameDataToBuiltinDefault(out string errorMessage)
    {
        try
        {
            // 默认配置优先从内置资源加载，失败时由场景定义回退到代码模板。
            var fallback = _gameDataSceneDefinition.CreateDefaultProfile();
            return TrySaveGameDataProfile(fallback, out errorMessage);
        }
        catch (Exception ex)
        {
            errorMessage = string.Empty;
            errorMessage = string.Format(I18nHelper.GetLocalizedString("SmartBpRegionConfigResetFailedFormat"), ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public SmartBpAspectInfo GetAspectInfo(string? captureAspectRatio)
    {
        _ = EnsureLoaded();
        var configAspect = string.IsNullOrWhiteSpace(_cachedProfile.BaseAspectRatio)
            ? ToAspectRatioText(_cachedProfile.BaseSize.Width, _cachedProfile.BaseSize.Height)
            : _cachedProfile.BaseAspectRatio;

        // 比例匹配只用于提示，不阻断流程。
        var isMatched = false;
        if (!string.IsNullOrWhiteSpace(captureAspectRatio) && captureAspectRatio != "-")
        {
            isMatched = IsAspectMatched(configAspect, captureAspectRatio);
        }

        return new SmartBpAspectInfo
        {
            ConfigPath = GameDataConfigPath,
            ConfigAspectRatio = configAspect,
            CurrentCaptureAspectRatio = string.IsNullOrWhiteSpace(captureAspectRatio) ? "-" : captureAspectRatio,
            IsMatched = isMatched
        };
    }

    /// <summary>
    /// 将像素尺寸转换为可读比例文本（如 16:9）。
    /// 会优先吸附到常见比例，再做小整数近似化简。
    /// </summary>
    public static string ToAspectRatioText(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return "-";

        var ratio = (double)width / height;
        var nearest = CommonAspects
            .Select(a => new { a.W, a.H, Diff = Math.Abs((double)a.W / a.H - ratio) })
            .OrderBy(x => x.Diff)
            .FirstOrDefault();
        if (nearest is not null && nearest.Diff <= AspectSnapTolerance)
            return $"{nearest.W}:{nearest.H}";

        // 通用化简：将任意比例近似为小整数比，避免出现 1418:771 这类不易阅读的结果。
        if (TryApproximateAspectRatio(ratio, AspectApproxMaxDenominator, out var aw, out var ah))
            return $"{aw}:{ah}";

        // 统一约分，避免 1920:1080 与 16:9 展示不一致。
        var gcd = Gcd(width, height);
        return $"{width / gcd}:{height / gcd}";
    }

    /// <summary>
    /// 将浮点尺寸转换为比例文本。
    /// </summary>
    public static string ToAspectRatioText(double width, double height)
    {
        return ToAspectRatioText((int)Math.Round(width), (int)Math.Round(height));
    }

    /// <summary>
    /// 将尺寸转换为“比例基准尺寸”（如 16x9），用于配置存储展示。
    /// </summary>
    public static WindowSize ToAspectBaseSize(int width, int height)
    {
        var aspect = ToAspectRatioText(width, height);
        if (TryParseAspectIntegerPair(aspect, out var w, out var h))
            return new WindowSize(w, h);

        return new WindowSize(Math.Max(1, width), Math.Max(1, height));
    }

    /// <summary>
    /// 计算两个整数的最大公约数。
    /// </summary>
    /// <param name="a">第一个整数。</param>
    /// <param name="b">第二个整数。</param>
    /// <returns>最大公约数。</returns>
    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            var t = a % b;
            a = b;
            b = t;
        }

        return Math.Abs(a);
    }

    /// <summary>
    /// 判断配置比例与当前捕获比例是否在容差内匹配。
    /// </summary>
    /// <param name="configAspect">配置比例文本。</param>
    /// <param name="captureAspect">捕获比例文本。</param>
    /// <returns>匹配返回 <see langword="true"/>。</returns>
    private static bool IsAspectMatched(string configAspect, string captureAspect)
    {
        var r1 = ParseAspect(configAspect);
        var r2 = ParseAspect(captureAspect);
        if (r1 <= 0 || r2 <= 0)
            return false;

        // 容差匹配：规避浮点和采样引入的小误差。
        return Math.Abs(r1 - r2) <= 0.01;
    }

    /// <summary>
    /// 解析形如 <c>16:9</c> 的比例文本。
    /// </summary>
    /// <param name="text">比例文本。</param>
    /// <returns>比例值；解析失败返回 -1。</returns>
    private static double ParseAspect(string text)
    {
        var parts = text.Split(':');
        if (parts.Length != 2)
            return -1;

        if (!double.TryParse(parts[0], out var w) || !double.TryParse(parts[1], out var h) || h <= 0)
            return -1;

        return w / h;
    }

    /// <summary>
    /// 尝试从比例文本中解析整数宽高。
    /// </summary>
    /// <param name="text">比例文本。</param>
    /// <param name="w">解析出的宽。</param>
    /// <param name="h">解析出的高。</param>
    /// <returns>解析成功返回 <see langword="true"/>。</returns>
    private static bool TryParseAspectIntegerPair(string text, out int w, out int h)
    {
        w = 0;
        h = 0;
        var parts = text.Split(':');
        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], out w) || !int.TryParse(parts[1], out h))
            return false;

        return w > 0 && h > 0;
    }

    /// <summary>
    /// 将浮点比例近似为较小整数比。
    /// </summary>
    /// <param name="ratio">浮点比例。</param>
    /// <param name="maxDenominator">允许的最大分母。</param>
    /// <param name="w">近似宽。</param>
    /// <param name="h">近似高。</param>
    /// <returns>找到足够接近的小整数比返回 <see langword="true"/>。</returns>
    private static bool TryApproximateAspectRatio(double ratio, int maxDenominator, out int w, out int h)
    {
        w = 0;
        h = 0;
        if (ratio <= 0 || maxDenominator < 1)
            return false;

        var bestError = double.MaxValue;
        var bestW = 0;
        var bestH = 0;

        for (var den = 1; den <= maxDenominator; den++)
        {
            var num = (int)Math.Round(ratio * den);
            if (num <= 0)
                continue;

            var approx = (double)num / den;
            var error = Math.Abs(approx - ratio) / ratio;
            if (error >= bestError)
                continue;

            var gcd = Gcd(num, den);
            bestW = num / gcd;
            bestH = den / gcd;
            bestError = error;
        }

        if (bestW <= 0 || bestH <= 0 || bestError > AspectApproxRelativeTolerance)
            return false;

        w = bestW;
        h = bestH;
        return true;
    }

    /// <summary>
    /// 确保配置已从用户文件加载；缺失或损坏时回退并写入默认配置。
    /// </summary>
    /// <returns>成功加载或重置返回 <see langword="true"/>。</returns>
    private bool EnsureLoaded()
    {
        Directory.CreateDirectory(ConfigDirectoryPath);

        // 用户自定义配置优先：只要用户文件存在，就优先读取它。
        if (!File.Exists(GameDataConfigPath))
        {
            // 首次运行：直接生成用户配置文件。
            return TryResetGameDataToBuiltinDefault(out _);
        }

        try
        {
            var json = File.ReadAllText(GameDataConfigPath);
            var profile = JsonSerializer.Deserialize<SmartBpRegionProfile>(json, _jsonOptions);
            if (profile != null && _gameDataSceneDefinition.TryValidateProfile(profile, out _))
            {
                _cachedProfile = profile;
                return true;
            }
        }
        catch
        {
            // 文件损坏/读取异常：下面会自动回退默认配置。
        }

        return TryResetGameDataToBuiltinDefault(out _);
    }

    /// <summary>
    /// 深拷贝区域配置 profile，避免调用方绕过校验修改缓存。
    /// </summary>
    /// <param name="profile">源 profile。</param>
    /// <returns>拷贝后的 profile。</returns>
    private static SmartBpRegionProfile DeepClone(SmartBpRegionProfile profile)
    {
        return new SmartBpRegionProfile
        {
            Version = profile.Version,
            Scene = profile.Scene,
            BaseAspectRatio = profile.BaseAspectRatio,
            BaseSize = new WindowSize(profile.BaseSize.Width, profile.BaseSize.Height),
            Layout = DeepCloneLayout(profile.Layout)
        };
    }

    /// <summary>
    /// 深拷贝区域布局定义。
    /// </summary>
    /// <param name="layout">源布局。</param>
    /// <returns>拷贝后的布局。</returns>
    private static RegionLayoutDefinition DeepCloneLayout(RegionLayoutDefinition layout)
    {
        return new RegionLayoutDefinition
        {
            SceneDisplayName = layout.SceneDisplayName,
            Roots = [.. layout.Roots.Select(CloneNode)]
        };
    }

    /// <summary>
    /// 递归拷贝区域布局节点。
    /// </summary>
    /// <param name="node">源节点。</param>
    /// <returns>拷贝后的节点。</returns>
    private static RegionLayoutNode CloneNode(RegionLayoutNode node)
    {
        return new RegionLayoutNode
        {
            Id = node.Id,
            Label = node.Label,
            NodeType = node.NodeType,
            TemplateGroupId = node.TemplateGroupId,
            Rect = node.Rect,
            ClampToParent = node.ClampToParent,
            Children = [.. node.Children.Select(CloneNode)]
        };
    }
}
