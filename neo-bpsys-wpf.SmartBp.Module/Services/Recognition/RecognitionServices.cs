using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 管理 SmartBP 识别区域 profile 的读取、保存和默认值回退。
/// </summary>
internal sealed class SmartBpRecognitionRegionProfileService(ISmartBpModuleStorageProvider storage) : ISmartBpRecognitionRegionProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private string BundledPath => Path.Combine(storage.ModuleRoot, "Resources", "SmartBp", "BpRecognitionLayoutProfile.json");
    private static string UserPath => Path.Combine(AppConstants.AppDataPath, "SmartBp", "BpRecognitionLayoutProfile.json");

    public async Task<SmartBpRecognitionLayoutProfile> LoadAsync(CancellationToken cancellationToken = default)
    {
        var isUserLayout = File.Exists(UserPath);
        var path = isUserLayout ? UserPath : BundledPath;
        await using var stream = File.OpenRead(path);
        var profile = await JsonSerializer.DeserializeAsync<SmartBpRecognitionLayoutProfile>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("SmartBP recognition layout profile is empty.");
        EnsureStatusRegions(profile);
        Validate(profile);
        profile.RuntimeSource = isUserLayout ? "user-layout" : "default";
        return profile;
    }

    public async Task SaveUserOverrideAsync(SmartBpRecognitionLayoutProfile profile, CancellationToken cancellationToken = default)
    {
        EnsureStatusRegions(profile);
        Validate(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(UserPath)!);
        await using var stream = File.Create(UserPath);
        await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, cancellationToken);
    }

    public Task ResetUserOverrideAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(UserPath)) File.Delete(UserPath);
        return Task.CompletedTask;
    }

    private static void Validate(SmartBpRecognitionLayoutProfile profile)
    {
        if (profile.SchemaVersion != 1) throw new InvalidDataException("Unsupported SmartBP recognition layout profile schema.");
        foreach (var key in new[] { "phase_top", "top_center_status", "top_left_status", "left_top", "right_top", "left_bottom", "right_bottom" })
        {
            if (!profile.Regions.TryGetValue(key, out var rect)) throw new InvalidDataException($"Missing SmartBP recognition region: {key}.");
            if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0 || rect.X + rect.Width > 1.0001 || rect.Y + rect.Height > 1.0001)
                throw new InvalidDataException($"SmartBP recognition region {key} is outside normalized bounds.");
        }
    }

    private static void EnsureStatusRegions(SmartBpRecognitionLayoutProfile profile)
    {
        profile.Regions.TryAdd("top_center_status", new SmartBpRecognitionRegionRect
        {
            X = .275,
            Y = .01,
            Width = .45,
            Height = .14
        });
        profile.Regions.TryAdd("top_left_status", new SmartBpRecognitionRegionRect
        {
            X = 0,
            Y = 0,
            Width = .36,
            Height = .11
        });
    }
}

/// <summary>
/// 按当前区域 profile 从前台截图裁剪识别所需的业务区域。
/// </summary>
internal sealed class SmartBpRecognitionFrameCropper(ISmartBpRecognitionRegionProfileService profileService) : ISmartBpRecognitionFrameCropper
{
    public BitmapSource Crop(BitmapSource source, SmartBpRecognitionRegion region) => CropWithInfo(source, region).Image;

    public SmartBpCroppedFrame CropWithInfo(BitmapSource source, SmartBpRecognitionRegion region)
    {
        var profile = profileService.LoadAsync().GetAwaiter().GetResult();
        var rect = profile.Regions[ToProfileKey(region)];
        using var sourceMat = BitmapSourceConverter.ToMat(source);
        var roi = ToPixelRect(rect, sourceMat.Width, sourceMat.Height);
        using var cropped = new Mat(sourceMat, roi).Clone();
        var image = BitmapSourceConverter.ToBitmapSource(cropped);
        image.Freeze();
        return new(region, image, roi.X, roi.Y, roi.Width, roi.Height)
        {
            LayoutSource = profile.RuntimeSource,
            NormalizedRectText = $"x={rect.X:0.####}, y={rect.Y:0.####}, w={rect.Width:0.####}, h={rect.Height:0.####}"
        };
    }

    private static Rect ToPixelRect(SmartBpRecognitionRegionRect rect, int width, int height)
    {
        var x = Math.Clamp((int)Math.Floor(rect.X * width), 0, Math.Max(0, width - 1));
        var y = Math.Clamp((int)Math.Floor(rect.Y * height), 0, Math.Max(0, height - 1));
        var right = Math.Clamp((int)Math.Ceiling((rect.X + rect.Width) * width), x + 1, width);
        var bottom = Math.Clamp((int)Math.Ceiling((rect.Y + rect.Height) * height), y + 1, height);
        return new Rect(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
    }

    private static string ToProfileKey(SmartBpRecognitionRegion region) => region switch
    {
        SmartBpRecognitionRegion.PhaseTop => "phase_top",
        SmartBpRecognitionRegion.TopCenterStatus => "top_center_status",
        SmartBpRecognitionRegion.TopLeftStatus => "top_left_status",
        SmartBpRecognitionRegion.LeftTop => "left_top",
        SmartBpRecognitionRegion.RightTop => "right_top",
        SmartBpRecognitionRegion.LeftBottom => "left_bottom",
        SmartBpRecognitionRegion.RightBottom => "right_bottom",
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, null)
    };
}

/// <summary>
/// 将模型输出的角色名称解析到当前共享数据中的规范角色名称。
/// </summary>
internal sealed class SmartBpCharacterResolver(ICharacterSelectionService characterSelectionService) : ISmartBpCharacterResolver
{
    public SmartBpNormalizedCharacter Resolve(string? rawName, Camp camp, int slot, double confidence)
    {
        if (SmartBpBusinessStateParser.IsUnselected(rawName))
            return new(rawName, null, null, camp, slot, confidence, []);

        var result = characterSelectionService.ResolveCharacterDetailed(rawName ?? string.Empty, camp);
        string[] warnings = result.Character == null
            ? [$"Unresolved character: {rawName}; matchMode={result.MatchMode}; score={result.Score:0.00}; reason={result.Reason}"]
            : [];
        var reason = $"matchMode={result.MatchMode}; score={result.Score:0.00}; safe={result.IsAutoApplySafe}; reason={result.Reason}";
        return new(
            rawName,
            result.CanonicalName,
            result.CanonicalName,
            camp,
            slot,
            Math.Min(confidence, result.Character == null ? .89 : Math.Max(confidence, result.Score)),
            warnings,
            result.MatchMode,
            result.IsAutoApplySafe,
            reason);
    }
}

/// <summary>
/// 将 OCR 识别到的玩家 ID 文本匹配到当前对局内部求生者玩家位置。
/// 支持精确匹配、去噪归一化匹配和高阈值非模糊模糊匹配；模糊匹配不用于自动应用。
/// </summary>
internal sealed class SmartBpPlayerIdentityMatcher(ISharedDataService shared) : ISmartBpPlayerIdentityMatcher
{
    private const double SafeThreshold = 0.85;
    private const double AmbiguityMargin = 0.10;

    /// <inheritdoc />
    public SmartBpPlayerIdentityMatchResult MatchSurvivorPlayer(string? rawPlayerId)
    {
        if (string.IsNullOrWhiteSpace(rawPlayerId))
            return SmartBpPlayerIdentityMatchResult.Unmatched("player_id missing or empty.");

        var players = shared.CurrentGame.SurPlayerList;
        var raw = rawPlayerId.Trim();
        var normalizedRaw = Normalize(raw);

        var scored = new List<(int Index, string MatchText, double Score, string Mode)>();
        for (var i = 0; i < players.Count; i++)
        {
            var member = players[i].Member;
            // 优先使用 GameId 匹配；GameId 为空时回退 Name
            var matchText = !string.IsNullOrWhiteSpace(member?.GameId) ? member.GameId : member?.Name;
            if (string.IsNullOrWhiteSpace(matchText))
                continue;
            var trimmedMatchText = matchText.Trim();
            var (score, mode) = ScoreMatch(normalizedRaw, Normalize(trimmedMatchText), raw, trimmedMatchText);
            if (score > 0)
                scored.Add((i, trimmedMatchText, score, mode));
        }

        if (scored.Count == 0)
            return SmartBpPlayerIdentityMatchResult.Unmatched($"no survivor player matched '{rawPlayerId}'.");

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));
        var best = scored[0];
        if (best.Score < SafeThreshold)
            return SmartBpPlayerIdentityMatchResult.Unmatched($"best match '{best.MatchText}' score={best.Score:0.00} below safe threshold {SafeThreshold:0.00}; mode={best.Mode}.");

        if (scored.Count > 1)
        {
            var second = scored[1];
            if (second.Score >= SafeThreshold && best.Score - second.Score < AmbiguityMargin)
                return SmartBpPlayerIdentityMatchResult.Unmatched(
                    $"ambiguous match: '{best.MatchText}' score={best.Score:0.00} vs '{second.MatchText}' score={second.Score:0.00}; rejected.");
        }

        return new(true, best.Index, best.MatchText, best.Score, true, $"matched mode={best.Mode}; score={best.Score:0.00}.");
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch)) continue;
            if (ch == '_' || ch == '-' || ch == '.' || ch == ',' || ch == '·' || ch == '•' || ch == '\'' || ch == '"') continue;
            builder.Append(char.ToLowerInvariant(ch));
        }
        return builder.ToString();
    }

    private static (double Score, string Mode) ScoreMatch(string normalizedRaw, string normalizedName, string raw, string name)
    {
        if (string.Equals(raw, name, StringComparison.Ordinal))
            return (1.0, "exact");
        if (string.Equals(normalizedRaw, normalizedName, StringComparison.Ordinal))
            return (0.98, "normalized");
        return (LevenshteinSimilarity(normalizedRaw, normalizedName), "fuzzy");
    }

    private static double LevenshteinSimilarity(string left, string right)
    {
        var maxLen = Math.Max(left.Length, right.Length);
        if (maxLen == 0) return 1.0;
        return 1.0 - (double)LevenshteinDistance(left, right) / maxLen;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        if (left.Length == 0) return right.Length;
        if (right.Length == 0) return left.Length;
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var j = 0; j <= right.Length; j++) previous[j] = j;
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}
