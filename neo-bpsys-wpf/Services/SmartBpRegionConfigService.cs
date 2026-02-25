using System.IO;
using System.Text.Json;
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

    private SmartBpRegionProfile _cachedProfile;

    /// <summary>
    /// 初始化配置服务并加载当前配置（失败时自动回退默认配置）。
    /// </summary>
    public SmartBpRegionConfigService()
    {
        // 先放一份内存默认值，确保任意异常下都有可用配置。
        _cachedProfile = SmartBpRegionDefaults.CreateGameDataDefaultProfile();
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
        if (!TryValidate(profile, out errorMessage))
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
        errorMessage = string.Empty;
        try
        {
            var source = Path.Combine(AppConstants.ResourcesPath, SmartBpRegionDefaults.BuiltinGameDataDefaultRelativePath);
            if (File.Exists(source))
            {
                Directory.CreateDirectory(ConfigDirectoryPath);
                // 重置是覆盖用户文件，不走合并。
                File.Copy(source, GameDataConfigPath, true);

                var json = File.ReadAllText(GameDataConfigPath);
                var profile = JsonSerializer.Deserialize<SmartBpRegionProfile>(json, _jsonOptions);
                if (profile != null && TryValidate(profile, out _))
                {
                    _cachedProfile = DeepClone(profile);
                    GameDataProfileChanged?.Invoke(this, EventArgs.Empty);
                    return true;
                }
            }

            // 如果资源文件丢失，回退到代码内默认模板兜底。
            var fallback = SmartBpRegionDefaults.CreateGameDataDefaultProfile();
            return TrySaveGameDataProfile(fallback, out errorMessage);
        }
        catch (Exception ex)
        {
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

    private static bool IsAspectMatched(string configAspect, string captureAspect)
    {
        var r1 = ParseAspect(configAspect);
        var r2 = ParseAspect(captureAspect);
        if (r1 <= 0 || r2 <= 0)
            return false;

        // 容差匹配：规避浮点和采样引入的小误差。
        return Math.Abs(r1 - r2) <= 0.01;
    }

    private static double ParseAspect(string text)
    {
        var parts = text.Split(':');
        if (parts.Length != 2)
            return -1;

        if (!double.TryParse(parts[0], out var w) || !double.TryParse(parts[1], out var h) || h <= 0)
            return -1;

        return w / h;
    }

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

    private bool EnsureLoaded()
    {
        Directory.CreateDirectory(ConfigDirectoryPath);

        if (!File.Exists(GameDataConfigPath))
        {
            // 首次运行：直接生成用户配置文件。
            return TryResetGameDataToBuiltinDefault(out _);
        }

        try
        {
            var json = File.ReadAllText(GameDataConfigPath);
            var profile = JsonSerializer.Deserialize<SmartBpRegionProfile>(json, _jsonOptions);
            if (profile != null && TryValidate(profile, out _))
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

    private static SmartBpRegionProfile DeepClone(SmartBpRegionProfile profile)
    {
        return new SmartBpRegionProfile
        {
            Version = profile.Version,
            Scene = profile.Scene,
            BaseAspectRatio = profile.BaseAspectRatio,
            BaseSize = new WindowSize(profile.BaseSize.Width, profile.BaseSize.Height),
            Rows =
            [
                .. profile.Rows.Select(row => new SmartBpRegionRow
                {
                    Id = row.Id,
                    BigRect = row.BigRect,
                    Cells =
                    [
                        .. row.Cells.Select(cell => new SmartBpRegionCell
                        {
                            Id = cell.Id,
                            Rect = cell.Rect
                        })
                    ]
                })
            ]
        };
    }

    private static bool TryValidate(SmartBpRegionProfile profile, out string errorMessage)
    {
        errorMessage = string.Empty;
        // 当前服务只处理 GameData 场景。
        if (!string.Equals(profile.Scene, "GameData", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = I18nHelper.GetLocalizedString("SmartBpRegionConfigInvalidScene");
            return false;
        }

        if (profile.Rows.Count != 5)
        {
            errorMessage = I18nHelper.GetLocalizedString("SmartBpRegionConfigInvalidRowCount");
            return false;
        }

        for (var i = 0; i < profile.Rows.Count; i++)
        {
            var row = profile.Rows[i];
            if (!row.BigRect.IsValid01())
            {
                errorMessage = string.Format(I18nHelper.GetLocalizedString("SmartBpRegionConfigInvalidBigRectFormat"), i + 1);
                return false;
            }

            if (row.Cells.Count != 6)
            {
                errorMessage = string.Format(I18nHelper.GetLocalizedString("SmartBpRegionConfigInvalidCellCountFormat"), i + 1);
                return false;
            }

            for (var c = 0; c < row.Cells.Count; c++)
            {
                // 小框坐标是相对大框，因此同样用 0~1 校验。
                if (!row.Cells[c].Rect.IsValid01())
                {
                    errorMessage = string.Format(I18nHelper.GetLocalizedString("SmartBpRegionConfigInvalidCellRectFormat"), i + 1, c + 1);
                    return false;
                }
            }
        }

        profile.BaseAspectRatio = string.IsNullOrWhiteSpace(profile.BaseAspectRatio)
            ? ToAspectRatioText(profile.BaseSize.Width, profile.BaseSize.Height)
            : profile.BaseAspectRatio;

        // 仅保留“比例基准尺寸”（如 16x9），避免把具体像素分辨率写入配置。
        if (TryParseAspectIntegerPair(profile.BaseAspectRatio, out var aw, out var ah))
            profile.BaseSize = new WindowSize(aw, ah);

        return true;
    }
}
