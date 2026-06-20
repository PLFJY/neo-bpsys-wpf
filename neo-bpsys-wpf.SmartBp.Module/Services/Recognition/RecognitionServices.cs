using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal sealed class SmartBpImageEncoder : ISmartBpImageEncoder
{
    public string EncodeDataUrl(BitmapSource source, int maxWidth)
    {
        BitmapSource image = source;
        if (source.PixelWidth > maxWidth)
        {
            var scale = (double)maxWidth / source.PixelWidth;
            image = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        }
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream(); encoder.Save(stream);
        return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
    }
}

internal sealed class SmartBpRecognitionRegionProfileService(ISmartBpModuleStorageProvider storage) : ISmartBpRecognitionRegionProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private string BundledPath => Path.Combine(storage.ModuleRoot, "Resources", "SmartBp", "BpRecognitionLayoutProfile.json");
    private static string UserPath => Path.Combine(AppConstants.AppDataPath, "SmartBp", "BpRecognitionLayoutProfile.json");

    public async Task<SmartBpRecognitionLayoutProfile> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = File.Exists(UserPath) ? UserPath : BundledPath;
        await using var stream = File.OpenRead(path);
        var profile = await JsonSerializer.DeserializeAsync<SmartBpRecognitionLayoutProfile>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("SmartBP recognition layout profile is empty.");
        Validate(profile);
        return profile;
    }

    public async Task SaveUserOverrideAsync(SmartBpRecognitionLayoutProfile profile, CancellationToken cancellationToken = default)
    {
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
        foreach (var key in new[] { "phase_top", "left_top", "right_top", "left_bottom", "right_bottom" })
        {
            if (!profile.Regions.TryGetValue(key, out var rect)) throw new InvalidDataException($"Missing SmartBP recognition region: {key}.");
            if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0 || rect.X + rect.Width > 1.0001 || rect.Y + rect.Height > 1.0001)
                throw new InvalidDataException($"SmartBP recognition region {key} is outside normalized bounds.");
        }
    }
}

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
        return new(region, image, roi.X, roi.Y, roi.Width, roi.Height);
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
        SmartBpRecognitionRegion.LeftTop => "left_top",
        SmartBpRecognitionRegion.RightTop => "right_top",
        SmartBpRecognitionRegion.LeftBottom => "left_bottom",
        SmartBpRecognitionRegion.RightBottom => "right_bottom",
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, null)
    };
}

internal sealed partial class SmartBpCharacterResolver(ISharedDataService shared) : ISmartBpCharacterResolver
{
    public SmartBpNormalizedCharacter Resolve(string? rawName, Camp camp, int slot, double confidence)
    {
        var warnings = new List<string>(); var dict = camp == Camp.Sur ? shared.SurCharaDict : shared.HunCharaDict;
        KeyValuePair<string, Core.Models.Character>? match = null;
        if (IsUnselected(rawName))
            return new(rawName, null, null, camp, slot, confidence, warnings);
        if (!string.IsNullOrWhiteSpace(rawName))
        {
            match = dict.FirstOrDefault(x => x.Key.Equals(rawName, StringComparison.Ordinal));
            var stripped = StripDecorativeQuotes(rawName);
            if (match.Value.Value == null) match = dict.FirstOrDefault(x => x.Key.Equals(stripped, StringComparison.Ordinal));
            if (match.Value.Value == null) match = dict.FirstOrDefault(x => x.Key.Equals(stripped.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match.Value.Value == null) { var normalized = Normalize(rawName); match = dict.FirstOrDefault(x => Normalize(x.Key) == normalized); }
        }
        if (match?.Value == null) warnings.Add(string.IsNullOrWhiteSpace(rawName) ? "Character was not visible or recognized." : $"Unresolved character: {rawName}");
        return new(rawName, match?.Value is null ? null : match.Value.Key, match?.Value?.Name, camp, slot, confidence, warnings);
    }
    private static bool IsUnselected(string? value) => string.Equals(value?.Trim(), "未选择", StringComparison.Ordinal);
    private static string Normalize(string value) => NonWordRegex().Replace(StripDecorativeQuotes(value), "").ToUpperInvariant();
    private static string StripDecorativeQuotes(string value)
    {
        var trimmed = value.Trim();
        var changed = true;
        while (changed && trimmed.Length >= 2)
        {
            changed = false;
            foreach (var (left, right) in QuotePairs)
            {
                if (trimmed[0] != left || trimmed[^1] != right) continue;
                trimmed = trimmed[1..^1].Trim();
                changed = true;
                break;
            }
        }
        return trimmed;
    }
    private static readonly (char Left, char Right)[] QuotePairs =
    [
        ('"', '"'), ('“', '”'), ('”', '“'), ('『', '』'), ('「', '」'), ('《', '》'), ('〈', '〉'), ('‘', '’'), ('\'', '\'')
    ];
    [GeneratedRegex(@"[\s\p{P}\p{S}]+", RegexOptions.CultureInvariant)] private static partial Regex NonWordRegex();
}

internal static class SmartBpRecognitionPromptBuilder
{
    public static string BuildSnapshotDelta(
        SmartBpSnapshotDeltaRequest request,
        IEnumerable<string> survivors,
        IEnumerable<string> hunters)
    {
        var fields = request.RequestedFields.ToArray();
        var mapping = string.Join(Environment.NewLine, request.RequestedRegions.Select((item, index) =>
            $"image_{index + 1} = {RegionId(item.Region)}, field={item.TargetField}"));
        return $$$"""
/no_think

你会收到多张第五人格 BP 裁剪图。
image_0 = phase_top，只用于判断 phase。
{{{mapping}}}

只输出一个 JSON：
{
  "phase": "...",
  "updates": [...]
}

requested_fields: {{{JsonSerializer.Serialize(fields)}}}
survivor_candidates: {{{JsonSerializer.Serialize(survivors)}}}
hunter_candidates: {{{JsonSerializer.Serialize(hunters)}}}

phase 只能是：
["屏蔽求生者","屏蔽监管者","选择求生者","求生者选择角色中","选择监管者","求生者选择天赋中","监管者选择天赋中","天赋已锁定","等待中","未知"]

区域规则：
- phase_top 只判断 phase，不识别角色。
- right_top 只用于 banned_sur。
- left_top 只用于 banned_hun。
- left_bottom 只用于 picked_sur。
- right_bottom 只用于 picked_hun。

只返回 requested_fields 中要求的字段更新；没有请求的字段不要输出。
如果某字段被请求，必须输出完整固定槽位数量。
如果 requested field 的裁剪图里仍可见上一阶段结果，即使当前 phase 已进入下一步，也必须输出该区域当前可见业务状态。
禁用符号、半透明、变暗表示已禁用，不是未选择。
character_name 必须是对应候选列表中的规范名称，或 "未选择"。
不要输出完整 BP 快照，除非所有字段都被请求。
不要输出 teams、all_characters、all_player_ids、scene、warnings、raw_visible_text、confidence、MapBP 字段。
""";
    }

    private static string RegionId(SmartBpRecognitionRegion region) => region switch
    {
        SmartBpRecognitionRegion.PhaseTop => "phase_top",
        SmartBpRecognitionRegion.LeftTop => "left_top",
        SmartBpRecognitionRegion.RightTop => "right_top",
        SmartBpRecognitionRegion.LeftBottom => "left_bottom",
        SmartBpRecognitionRegion.RightBottom => "right_bottom",
        _ => region.ToString()
    };

    public static string BuildPhaseRecognition() => """
/no_think

你只需要判断当前第五人格 BP 阶段。
你看到的是顶部操作区域裁剪图，包含左上和右上。
不要识别角色。
不要输出 ban/pick 槽位。
只输出 {"phase":"..."}。
phase 只能是：
["屏蔽求生者","屏蔽监管者","选择求生者","求生者选择角色中","选择监管者","求生者选择天赋中","监管者选择天赋中","天赋已锁定","等待中","未知"]
非活动侧的“等待中”不能决定 phase。
如果右上标题是“屏蔽求生者”，phase="屏蔽求生者"。
如果左上标题是“屏蔽监管者”，phase="屏蔽监管者"。
如果左侧/求生者方标题包含“选择天赋中”，phase="求生者选择天赋中"。
如果右侧/监管者方标题包含“选择天赋中”，phase="监管者选择天赋中"。
不要输出地图 BP。
""";

    public static string BuildFocusedBusiness(GameAction action, IEnumerable<string> survivors, IEnumerable<string> hunters)
    {
        var phase = SmartBpAutomaticMapping.ToPhase(action);
        var (region, targetField) = SmartBpAutomaticMapping.GetFocusedTarget(action);
        var regionText = region switch
        {
            SmartBpRecognitionRegion.RightTop => "右上角裁剪图",
            SmartBpRecognitionRegion.LeftTop => "左上角裁剪图",
            SmartBpRecognitionRegion.LeftBottom => "左下角裁剪图",
            SmartBpRecognitionRegion.RightBottom => "右下角裁剪图",
            _ => "裁剪图"
        };
        var instruction = action switch
        {
            GameAction.BanSur => "这个右上区域只负责 banned_sur。如果禁用求生者结果已经显示，就输出 banned_sur。不要因为全局阶段已经进入下一步，就把可见 ban 结果输出为“未选择”。禁用符号、变暗、半透明表示已禁用，不是未选择。",
            GameAction.BanHun => "这个左上区域只负责 banned_hun。如果禁用监管者结果已经显示，就输出 banned_hun。不要因为全局阶段已经进入下一步，就把可见 ban 结果输出为“未选择”。禁用符号、变暗、半透明表示已禁用，不是未选择。",
            GameAction.PickSur => "这个左下区域可能处于选择求生者、求生者选择角色中、求生者选择天赋中或天赋已锁定。无论当前阶段标题是什么，只要求生者角色槽仍然可见，就要输出 picked_sur；玩家 ID 可见时也要填入 player_id。",
            GameAction.DistributeChara => "这个左下区域只负责 picked_sur。即使全局阶段已经进入天赋选择，只要角色分配和玩家 ID 仍可见，就必须输出最终可见状态。",
            GameAction.PickHun => "这个右下区域可能处于选择监管者、监管者选择天赋中或天赋已锁定。无论当前阶段标题是什么，只要监管者角色和玩家 ID 仍然可见，就要输出 picked_hun。监管者头像下通常第一行是角色名，第二行是玩家 ID。",
            _ => throw new NotSupportedException($"Focused business extraction does not support {action}.")
        };
        return $$$"""
/no_think

你看到的是{{{regionText}}}。图片已经由程序裁剪，只能识别这个裁剪区域。
即使当前全局阶段已经进入下一步，这个区域中仍然可能保留上一阶段的结果。请识别该区域当前可见的最终业务状态，不要因为标题已经变化就丢掉可见角色结果。
phase="{{{phase}}}"
target_field="{{{targetField}}}"
{{{instruction}}}

survivor_candidates: {{{JsonSerializer.Serialize(survivors)}}}
hunter_candidates: {{{JsonSerializer.Serialize(hunters)}}}

character_name 必须是对应阵营候选列表里的规范名称，或 "未选择"。
如果角色名可读且匹配候选角色名，必须输出角色名；不要因为禁用符号、变暗、半透明输出为“未选择”。
如果屏幕官方名称带装饰性引号，但候选列表中是不带引号的规范名，输出候选列表中的规范名。
玩家 ID 只能出现在 player_id 字段。
不要输出地图 BP，不要输出 MapBP，不要输出 teams/all_characters/all_player_ids/warnings/confidence/raw_visible_text。
""";
    }

    public static string BuildStageDetection() => """
/no_think
只判断当前 BP 阶段，不要列出角色。
布局固定：left_top=求生者方禁用监管者；left_bottom=求生者选择或角色分配；right_top=监管者方禁用求生者；right_bottom=监管者选择。
左亮右暗表示求生者方操作，右亮左暗表示监管者方操作。标题文字优先于亮度。
屏蔽监管者=>BanHun/left/left_top/survivor/hunter；屏蔽求生者=>BanSur/right/right_top/hunter/survivor；选择求生者=>PickSur/left/left_bottom/survivor/survivor；求生者选择角色中=>DistributeChara/left/left_bottom/survivor/survivor；选择监管者=>PickHun/right/right_bottom/hunter/hunter。
不要输出内部步骤索引，不要输出角色，不要输出地图 BP。
""";

    public static string BuildFocused(GameAction action, IReadOnlyList<int> indexes, IEnumerable<string> survivors, IEnumerable<string> hunters)
    {
        var (region, camp, meaning) = SmartBpAutomaticMapping.Get(action);
        return $"""
/no_think
Current app step: {action}
Current step indexes: {JsonSerializer.Serialize(indexes)}
Only inspect {region}.
Target camp: {camp}.
Business meaning: {meaning}
Return only slots belonging to this operation. Keep player_id separate from character_name. Character names must come from the matching candidate list.
survivor_candidates: {JsonSerializer.Serialize(survivors)}
hunter_candidates: {JsonSerializer.Serialize(hunters)}
Do not output unrelated bans, picks, talents, or map data.
""";
    }
    public static string Build(SmartBpRecognitionTask task, IEnumerable<string> survivors, IEnumerable<string> hunters)
    {
        var description = task switch
        {
            SmartBpRecognitionTask.BanSur => "测试图参考：右上标题通常是“屏蔽求生者”，但仍必须从截图读取 phase。",
            SmartBpRecognitionTask.BanHun => "测试图参考：左上标题通常是“屏蔽监管者”，但仍必须从截图读取 phase。",
            SmartBpRecognitionTask.PickSur => "测试图参考：左上标题通常是“选择求生者”，但仍必须从截图读取 phase。",
            SmartBpRecognitionTask.PickHun => "测试图参考：右上标题通常是“选择监管者”，但仍必须从截图读取 phase。",
            SmartBpRecognitionTask.CharacterDistribution => "测试图参考：左上标题通常是“求生者选择角色中”，但仍必须从截图读取 phase。",
            _ => "从当前截图识别完整 BP 业务状态。"
        };
        return $$$"""
/no_think

请从截图识别当前第五人格 BP 业务状态，输出严格 JSON。

task_hint: {{{description}}}

survivor_candidates: {{{JsonSerializer.Serialize(survivors)}}}
hunter_candidates: {{{JsonSerializer.Serialize(hunters)}}}

phase 只能是：
["屏蔽求生者","屏蔽监管者","选择求生者","求生者选择角色中","选择监管者","求生者选择天赋中","监管者选择天赋中","天赋已锁定","等待中","未知"]

重要：禁用符号不是未选择。
如果角色头像或文字旁边有红色禁止符号 / 不可选标记，并且角色名可读，说明该角色已经被 ban，必须输出角色名。
只有屏幕文字真的显示“未选择”时，才输出“未选择”。

ban-sur:
右上区域 = banned_sur。
右上标题“屏蔽求生者”时，不要读取左侧“等待中”作为 phase。
右上槽位可读角色必须写入 banned_sur。

ban-hun:
左上区域 = banned_hun。
左上标题“屏蔽监管者”时，不要读取右侧“等待中”作为 phase。
左上槽位可读角色必须写入 banned_hun。

输出 JSON 只能有这些根字段：
phase, banned_sur, banned_hun, picked_sur, picked_hun

字段要求：
- banned_sur 固定 4 项，index 0..3，来自右上区域。
- banned_hun 固定 2 项，index 0..1，来自左上区域。
- picked_sur 固定 4 项，index 0..3，来自左下区域。
- picked_hun 固定一个对象，index=0，来自右下区域。
- character_name 必须是候选列表中的规范名称，或 "未选择"。
- 如果屏幕文字带装饰性引号，但候选名没有引号，输出候选名本身。
- player_id 必须和对应 picked_sur / picked_hun 槽位绑定。

阶段参考：
- 右上标题是“屏蔽求生者” => phase="屏蔽求生者"，填写 banned_sur。
- 左上标题是“屏蔽监管者” => phase="屏蔽监管者"，填写 banned_hun。
- 左上标题是“选择求生者” => phase="选择求生者"，填写 picked_sur。
- 左上标题是“求生者选择角色中” => phase="求生者选择角色中"，填写 picked_sur 的角色和玩家 ID。
- 右上标题是“选择监管者” => phase="选择监管者"，填写 picked_hun。
- 左侧/求生者方标题包含“选择天赋中” => phase="求生者选择天赋中"，不生成角色变更。
- 右侧/监管者方标题包含“选择天赋中” => phase="监管者选择天赋中"，不生成角色变更。
- 画面出现“天赋已锁定” => phase="天赋已锁定"，不生成角色变更。
- 非活动侧的“等待中”忽略。

反错误规则：
- 不要把 ban-sur 图识别为“等待中”：如果右上有“屏蔽求生者”，就是“屏蔽求生者”。
- 不要把可读角色输出为“未选择”：头像下方文字能读出并匹配候选角色时，必须输出角色名。
- 不要把监管者玩家 ID 输出 null：右下监管者头像下方第二行可见时，必须填入 picked_hun.player_id。
- 不要输出 teams、all_characters、all_player_ids、scene、warnings、raw_visible_text、confidence、MapBP 字段。
""";
    }
}

internal static class SmartBpRecognitionJsonSchemaProvider
{
    public static JsonObject GetPhaseOnly() =>
        Object(new JsonObject { ["phase"] = Phase() }, "phase");

    public static JsonObject GetFocusedBusiness(
        GameAction action,
        IReadOnlyList<string> survivorCandidates,
        IReadOnlyList<string> hunterCandidates)
    {
        var survivorNames = CharacterNameEnum(survivorCandidates);
        var hunterNames = CharacterNameEnum(hunterCandidates);
        var phase = SmartBpAutomaticMapping.ToPhase(action);
        var (_, targetField) = SmartBpAutomaticMapping.GetFocusedTarget(action);
        return action switch
        {
            GameAction.BanSur => Object(new JsonObject
            {
                ["phase"] = Const(phase),
                ["target_field"] = Const(targetField),
                ["slots"] = FixedArray(CharacterSlot(survivorNames, 0, 1, 2, 3), 4)
            }, "phase", "target_field", "slots"),
            GameAction.BanHun => Object(new JsonObject
            {
                ["phase"] = Const(phase),
                ["target_field"] = Const(targetField),
                ["slots"] = FixedArray(CharacterSlot(hunterNames, 0, 1), 2)
            }, "phase", "target_field", "slots"),
            GameAction.PickSur or GameAction.DistributeChara => Object(new JsonObject
            {
                ["phase"] = Const(phase),
                ["target_field"] = Const(targetField),
                ["slots"] = FixedArray(PlayerCharacterSlot(survivorNames, 0, 1, 2, 3), 4)
            }, "phase", "target_field", "slots"),
            GameAction.PickHun => Object(new JsonObject
            {
                ["phase"] = Const(phase),
                ["target_field"] = Const(targetField),
                ["picked_hun"] = Object(new JsonObject { ["index"] = Const(0), ["character_name"] = hunterNames, ["player_id"] = NullableString() }, "index", "character_name", "player_id")
            }, "phase", "target_field", "picked_hun"),
            _ => throw new NotSupportedException($"Focused business schema does not support {action}.")
        };
    }

    public static JsonObject GetSnapshotDelta(
        IReadOnlyCollection<string> requestedFields,
        IReadOnlyList<string> survivorCandidates,
        IReadOnlyList<string> hunterCandidates,
        bool strictCandidateEnums)
    {
        if (requestedFields.Count == 0)
            return Object(new JsonObject
            {
                ["phase"] = Phase(),
                ["updates"] = new JsonObject { ["type"] = "array", ["minItems"] = 0, ["maxItems"] = 0 }
            }, "phase", "updates");
        var survivorNames = strictCandidateEnums ? CharacterNameEnum(survivorCandidates) : StringCharacterName();
        var hunterNames = strictCandidateEnums ? CharacterNameEnum(hunterCandidates) : StringCharacterName();
        JsonObject update = Object(new JsonObject
        {
            ["field"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(requestedFields.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) },
            ["slots"] = new JsonObject
            {
                ["anyOf"] = new JsonArray(
                    new JsonObject { ["type"] = "null" },
                    FixedArray(PlayerCharacterSlot(survivorNames, 0, 1, 2, 3), 4),
                    FixedArray(PlayerCharacterSlot(hunterNames, 0, 1), 2))
            },
            ["picked_hun"] = new JsonObject
            {
                ["anyOf"] = new JsonArray(
                    new JsonObject { ["type"] = "null" },
                    Object(new JsonObject { ["index"] = Const(0), ["character_name"] = hunterNames.DeepClone(), ["player_id"] = NullableString() }, "index", "character_name", "player_id"))
            }
        }, "field", "slots", "picked_hun");
        return Object(new JsonObject
        {
            ["phase"] = Phase(),
            ["updates"] = Array(update)
        }, "phase", "updates");
    }

    public static JsonObject GetStageDetection()
    {
        JsonObject Enum(params string[] values) => new() { ["type"] = "string", ["enum"] = new JsonArray(values.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) };
        return Object(new JsonObject
        {
            ["schema_version"] = Const(1), ["recognized_action"] = Enum("BanSur", "BanHun", "PickSur", "DistributeChara", "PickHun", "Unknown"),
            ["active_side"] = Enum("left", "right", "unknown"), ["operation_region"] = Enum("left_top", "left_bottom", "right_top", "right_bottom", "unknown"),
            ["operation_owner"] = Enum("survivor", "hunter", "unknown"), ["target_camp"] = Enum("survivor", "hunter", "unknown"),
            ["left_top_title"] = NullableString(), ["right_top_title"] = NullableString(), ["main_status"] = NullableString(),
            ["confidence"] = Confidence(), ["evidence"] = StringArray(), ["warnings"] = StringArray()
        }, "schema_version", "recognized_action", "active_side", "operation_region", "operation_owner", "target_camp", "left_top_title", "right_top_title", "main_status", "confidence", "evidence", "warnings");
    }

    public static JsonObject GetFocused(GameAction action)
    {
        var task = action.ToString();
        var slot = Object(new JsonObject { ["slot_index"] = new JsonObject { ["type"] = "integer", ["minimum"] = -1 }, ["slot_state"] = SlotState(), ["character_name"] = NullableString(), ["player_id"] = NullableString(), ["is_banned_or_unavailable"] = new JsonObject { ["type"] = "boolean" }, ["raw_visible_text"] = NullableString(), ["confidence"] = Confidence() }, "slot_index", "slot_state", "character_name", "player_id", "is_banned_or_unavailable", "raw_visible_text", "confidence");
        var (region, camp, _) = SmartBpAutomaticMapping.Get(action);
        return Object(new JsonObject { ["schema_version"] = Const(1), ["task"] = Const(task), ["operation_region"] = Const(region), ["target_camp"] = Const(camp), ["slots"] = Array(slot), ["warnings"] = StringArray() }, "schema_version", "task", "operation_region", "target_camp", "slots", "warnings");
    }
    public static JsonObject Get(
        SmartBpRecognitionTask task,
        IReadOnlyList<string> survivorCandidates,
        IReadOnlyList<string> hunterCandidates)
    {
        var survivorNames = CharacterNameEnum(survivorCandidates);
        var hunterNames = CharacterNameEnum(hunterCandidates);
        var banSurSlot = CharacterSlot(survivorNames, 0, 1, 2, 3);
        var banHunSlot = CharacterSlot(hunterNames, 0, 1);
        var pickSurSlot = PlayerCharacterSlot(survivorNames, 0, 1, 2, 3);
        var pickHunSlot = Object(new JsonObject { ["index"] = Const(0), ["character_name"] = hunterNames.DeepClone(), ["player_id"] = NullableString() }, "index", "character_name", "player_id");
        return Object(new JsonObject
        {
            ["phase"] = Phase(),
            ["banned_sur"] = FixedArray(banSurSlot, 4),
            ["banned_hun"] = FixedArray(banHunSlot, 2),
            ["picked_sur"] = FixedArray(pickSurSlot, 4),
            ["picked_hun"] = pickHunSlot
        }, "phase", "banned_sur", "banned_hun", "picked_sur", "picked_hun");
    }
    public static JsonObject Get(SmartBpRecognitionTask task) => Get(task, [], []);
    private static JsonObject CharacterSlot(JsonObject characterName, params int[] indexes) => Object(new JsonObject { ["index"] = IntegerEnum(indexes), ["character_name"] = characterName.DeepClone() }, "index", "character_name");
    private static JsonObject PlayerCharacterSlot(JsonObject characterName, params int[] indexes) => Object(new JsonObject { ["index"] = IntegerEnum(indexes), ["character_name"] = characterName.DeepClone(), ["player_id"] = NullableString() }, "index", "character_name", "player_id");
    private static JsonObject CharacterNameEnum(IReadOnlyList<string> candidates)
    {
        var values = candidates
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Append("未选择")
            .Distinct(StringComparer.Ordinal)
            .Select(x => (JsonNode?)JsonValue.Create(x))
            .ToArray();
        return new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(values) };
    }
    private static JsonObject StringCharacterName() => new() { ["type"] = "string" };
    private static JsonObject Phase() => new() { ["type"] = "string", ["enum"] = new JsonArray("屏蔽求生者", "屏蔽监管者", "选择求生者", "求生者选择角色中", "选择监管者", "求生者选择天赋中", "监管者选择天赋中", "天赋已锁定", "等待中", "未知") };
    private static JsonObject IntegerEnum(params int[] values) => new() { ["type"] = "integer", ["enum"] = new JsonArray(values.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) };
    private static JsonObject FixedArray(JsonNode? item, int count) => new() { ["type"] = "array", ["minItems"] = count, ["maxItems"] = count, ["items"] = item };
    private static JsonObject Object(JsonObject properties, params string[] required) => new() { ["type"] = "object", ["additionalProperties"] = false, ["properties"] = properties, ["required"] = new JsonArray(required.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) };
    private static JsonObject Const(object value) => new() { ["const"] = JsonValue.Create(value) };
    private static JsonObject NullableString() => new() { ["type"] = new JsonArray("string", "null") };
    private static JsonObject Integer() => new() { ["type"] = "integer", ["minimum"] = 0 };
    private static JsonObject Confidence() => new() { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 };
    private static JsonObject Side() => new() { ["type"] = "string", ["enum"] = new JsonArray("left", "right", "top", "bottom", "unknown") };
    private static JsonObject Faction() => new() { ["type"] = "string", ["enum"] = new JsonArray("survivor", "hunter", "unknown") };
    private static JsonObject SlotState() => new() { ["type"] = "string", ["enum"] = new JsonArray("selected", "waiting", "unselected", "banned", "unknown") };
    private static JsonObject StringArray() => new() { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } };
    private static JsonObject Array(JsonNode? item) => new() { ["type"] = "array", ["items"] = item };
}

internal sealed class LlamaCppOpenAiClient(ISmartBpRecognitionSettingsService settings, ISharedDataService shared, ISmartBpPromptProfileProvider promptProfiles, ILogger<LlamaCppOpenAiClient> logger, ISmartBpDebugLog debugLog) : ILlamaCppOpenAiClient
{
    public async Task<string> RecognizeSnapshotDeltaAsync(IReadOnlyList<SmartBpMultimodalRegionInput> regions, SmartBpSnapshotDeltaRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await promptProfiles.LoadAsync(settings.Settings.PromptProfileId, cancellationToken);
        var needsSurvivors = request.RequestedFields.Any(field => field is "banned_sur" or "picked_sur");
        var needsHunters = request.RequestedFields.Any(field => field is "banned_hun" or "picked_hun");
        var survivorCandidates = needsSurvivors ? shared.SurCharaDict.Keys : Enumerable.Empty<string>();
        var hunterCandidates = needsHunters ? shared.HunCharaDict.Keys : Enumerable.Empty<string>();
        var content = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = SmartBpRecognitionPromptBuilder.BuildSnapshotDelta(request, survivorCandidates, hunterCandidates) } };
        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            content.Add(new JsonObject { ["type"] = "text", ["text"] = $"image_{i} = {region.Id}, purpose = {region.TargetField}" });
            content.Add(new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = region.ImageDataUrl } });
        }
        var body = new JsonObject
        {
            ["model"] = "local",
            ["temperature"] = 0,
            ["max_tokens"] = settings.Settings.SnapshotDeltaMaxTokens,
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = profile.SystemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = content }),
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = "smartbp_delta",
                    ["strict"] = true,
                    ["schema"] = SmartBpRecognitionJsonSchemaProvider.GetSnapshotDelta(request.RequestedFields, shared.SurCharaDict.Keys.ToArray(), shared.HunCharaDict.Keys.ToArray(), settings.Settings.UseStrictCandidateEnumsInAutoSchema)
                }
            }
        };
        return await SendSpecialAsync(body, $"SnapshotDelta:{string.Join(",", request.RequestedFields)}", cancellationToken);
    }

    public async Task<string> RecognizePhaseAsync(string imageDataUrl, CancellationToken cancellationToken = default)
    {
        var profile = await promptProfiles.LoadAsync(settings.Settings.PromptProfileId, cancellationToken);
        var body = CreateBody(profile.SystemPrompt, SmartBpRecognitionPromptBuilder.BuildPhaseRecognition(), imageDataUrl,
            SmartBpRecognitionJsonSchemaProvider.GetPhaseOnly(), settings.Settings.PhaseMaxTokens);
        return await SendSpecialAsync(body, "PhaseTop", cancellationToken);
    }

    public async Task<string> RecognizeFocusedBusinessAsync(string imageDataUrl, GameAction action, CancellationToken cancellationToken = default)
    {
        var profile = await promptProfiles.LoadAsync(settings.Settings.PromptProfileId, cancellationToken);
        var body = CreateBody(profile.SystemPrompt, SmartBpRecognitionPromptBuilder.BuildFocusedBusiness(action, shared.SurCharaDict.Keys, shared.HunCharaDict.Keys), imageDataUrl,
            SmartBpRecognitionJsonSchemaProvider.GetFocusedBusiness(action, shared.SurCharaDict.Keys.ToArray(), shared.HunCharaDict.Keys.ToArray()), settings.Settings.FocusedMaxTokens);
        return await SendSpecialAsync(body, $"FocusedBusiness:{action}", cancellationToken);
    }

    public async Task<string> DetectStageAsync(string imageDataUrl, CancellationToken cancellationToken = default)
    {
        var profile = await promptProfiles.LoadAsync(settings.Settings.PromptProfileId, cancellationToken);
        var body = CreateBody(profile.SystemPrompt, SmartBpRecognitionPromptBuilder.BuildStageDetection(), imageDataUrl,
            SmartBpRecognitionJsonSchemaProvider.GetStageDetection(), 512);
        return await SendSpecialAsync(body, "DetectStage", cancellationToken);
    }

    public async Task<string> RecognizeFocusedAsync(string imageDataUrl, GameAction action, IReadOnlyList<int> indexes, CancellationToken cancellationToken = default)
    {
        var profile = await promptProfiles.LoadAsync(settings.Settings.PromptProfileId, cancellationToken);
        var body = CreateBody(profile.SystemPrompt, SmartBpRecognitionPromptBuilder.BuildFocused(action, indexes, shared.SurCharaDict.Keys, shared.HunCharaDict.Keys), imageDataUrl,
            SmartBpRecognitionJsonSchemaProvider.GetFocused(action), settings.Settings.FocusedMaxTokens);
        return await SendSpecialAsync(body, action.ToString(), cancellationToken);
    }

    private static JsonObject CreateBody(string systemPrompt, string userPrompt, string imageDataUrl, JsonObject schema, int maxTokens) => new()
    {
        ["model"] = "local", ["temperature"] = 0, ["max_tokens"] = maxTokens,
        ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
        ["messages"] = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = systemPrompt }, new JsonObject { ["role"] = "user", ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = userPrompt }, new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = imageDataUrl } }) }),
        ["response_format"] = new JsonObject { ["type"] = "json_schema", ["json_schema"] = new JsonObject { ["name"] = "smartbp_result", ["strict"] = true, ["schema"] = schema } }
    };

    private async Task<string> SendSpecialAsync(JsonObject body, string taskLabel, CancellationToken token)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var url = $"http://127.0.0.1:{settings.Settings.LlamaServerPort}/v1/chat/completions";
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            debugLog.Write("recognition", $"POST {url}; task={taskLabel}; max_tokens={body["max_tokens"]}; attempt={attempt}/2");
            using var response = await http.PostAsync(url, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"), token).ConfigureAwait(false);
            var envelope = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new LlamaCppRequestException($"llama.cpp returned {(int)response.StatusCode}: {envelope}", envelope);
            using var document = JsonDocument.Parse(envelope);
            var choice = document.RootElement.GetProperty("choices")[0];
            var finish = choice.TryGetProperty("finish_reason", out var finishElement) ? finishElement.GetString() : null;
            if (finish == "length")
            {
                if (attempt == 1) { body["max_tokens"] = Math.Min((body["max_tokens"]?.GetValue<int>() ?? 512) * 2, 8192); continue; }
                throw new LlamaCppRequestException("llama.cpp exhausted the output token budget twice.", envelope);
            }
            var content = choice.GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content)) throw new LlamaCppRequestException("llama.cpp returned empty content.", envelope);
            return content;
        }
        throw new InvalidOperationException("Recognition retry loop ended unexpectedly.");
    }

    public async Task<string> RecognizeAsync(string imageDataUrl, SmartBpRecognitionTask task, CancellationToken cancellationToken = default)
    {
        var profile = await promptProfiles.LoadAsync(settings.Settings.PromptProfileId, cancellationToken);
        var initialMaxTokens = task is SmartBpRecognitionTask.BanSur or SmartBpRecognitionTask.BanHun or SmartBpRecognitionTask.PickSur or SmartBpRecognitionTask.PickHun ? settings.Settings.FocusedMaxTokens : settings.Settings.FullScanMaxTokens;
        var body = new JsonObject { ["model"] = "local", ["temperature"] = 0,
            ["max_tokens"] = initialMaxTokens,
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = profile.SystemPrompt }, new JsonObject { ["role"] = "user", ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = SmartBpRecognitionPromptBuilder.Build(task, shared.SurCharaDict.Keys, shared.HunCharaDict.Keys) }, new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = imageDataUrl } }) }),
            ["response_format"] = new JsonObject { ["type"] = "json_schema", ["json_schema"] = new JsonObject { ["name"] = "smartbp_result", ["strict"] = true, ["schema"] = SmartBpRecognitionJsonSchemaProvider.Get(task, shared.SurCharaDict.Keys.ToArray(), shared.HunCharaDict.Keys.ToArray()) } } };
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) }; var url = $"http://127.0.0.1:{settings.Settings.LlamaServerPort}/v1/chat/completions";
        logger.LogInformation("Recognition request started. Task={Task}", task);
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            debugLog.Write("recognition", $"POST {url}; task={task}; max_tokens={body["max_tokens"]}; attempt={attempt}/2");
            using var response = await http.PostAsync(url, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            debugLog.Write("recognition", $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}; response length={raw.Length}");
            if (!response.IsSuccessStatusCode) throw new LlamaCppRequestException($"llama.cpp returned {(int)response.StatusCode}: {raw}", raw);
            try
            {
                using var document = JsonDocument.Parse(raw);
                var choice = document.RootElement.GetProperty("choices")[0];
                var finishReason = choice.TryGetProperty("finish_reason", out var finish) ? finish.GetString() : null;
                if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
                {
                    if (attempt == 1)
                    {
                        var retryTokens = Math.Min((body["max_tokens"]?.GetValue<int>() ?? initialMaxTokens) * 2, 8192);
                        body["max_tokens"] = retryTokens;
                        debugLog.Write("recognition", $"Generation hit the token limit; retrying independently with max_tokens={retryTokens}.");
                        continue;
                    }
                    throw new LlamaCppRequestException("llama.cpp exhausted the output token budget twice and returned truncated JSON. Increase the recognition max token setting or reduce the extraction scope.", raw);
                }
                var message = choice.GetProperty("message");
                var content = message.TryGetProperty("content", out var contentElement)
                    ? contentElement.ValueKind == JsonValueKind.String ? contentElement.GetString() : contentElement.GetRawText()
                    : null;
                if (string.IsNullOrWhiteSpace(content) || content == "null")
                {
                    var reasoningLength = message.TryGetProperty("reasoning_content", out var reasoning) && reasoning.ValueKind == JsonValueKind.String
                        ? reasoning.GetString()?.Length ?? 0
                        : 0;
                    debugLog.Write("recognition", $"message.content is empty; reasoning_content length={reasoningLength}. Full envelope copied to Raw JSON.");
                    throw new LlamaCppRequestException("llama.cpp returned an empty message.content. Check the Raw JSON response and debug console; the model may have consumed the token budget without producing JSON.", raw);
                }
                debugLog.Write("recognition", $"Model JSON content length={content.Length}; finish_reason={finishReason ?? "unknown"}");
                return content;
            }
            catch (LlamaCppRequestException) { throw; }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                throw new LlamaCppRequestException($"Invalid OpenAI-compatible response envelope: {ex.Message}", raw);
            }
        }
        throw new InvalidOperationException("Recognition retry loop ended unexpectedly.");
    }
}

internal sealed class SmartBpAiRecognitionService(ISmartBpImageEncoder encoder, ILlamaCppOpenAiClient client,
    ISmartBpCharacterResolver resolver, ISmartBpRecognitionSettingsService settings, ILogger<SmartBpAiRecognitionService> logger) : ISmartBpAiRecognitionService
{
    public async Task<SmartBpRecognitionPreview> RecognizeAsync(BitmapSource frame, SmartBpRecognitionTask task, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew(); string raw = "";
        try
        {
            var imageDataUrl = await Task.Run(() => encoder.EncodeDataUrl(frame, settings.Settings.MaxImageWidth), cancellationToken).ConfigureAwait(false);
            raw = await client.RecognizeAsync(imageDataUrl, task, cancellationToken).ConfigureAwait(false);
            var (visual, resolved) = await Task.Run(() => Parse(raw, task), cancellationToken).ConfigureAwait(false);
            watch.Stop(); var recommended = Math.Clamp((int)Math.Ceiling(watch.ElapsedMilliseconds * 1.5), settings.Settings.MinRecognitionIntervalMs, settings.Settings.MaxRecognitionIntervalMs);
            logger.LogInformation("Recognition parsed successfully. Task={Task}, ElapsedMs={Elapsed}", task, watch.ElapsedMilliseconds);
            return new(raw, visual, resolved, watch.ElapsedMilliseconds, recommended, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { watch.Stop(); if (ex is LlamaCppRequestException request) raw = request.RawResponse; logger.LogWarning(ex, "Recognition parse/request failed"); return new(raw, "", "", watch.ElapsedMilliseconds, 0, ex.Message); }
    }

    internal (string VisualSummary, string ResolvedSummary) Parse(string raw, SmartBpRecognitionTask expected)
    {
        var result = JsonSerializer.Deserialize<SmartBpBusinessStateRecognitionResult>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = false })
            ?? throw new InvalidDataException("Recognition JSON is empty.");
        SmartBpBusinessStateParser.NormalizeAndValidate(result);

        var visual = new StringBuilder();
        var resolved = new StringBuilder();
        visual.AppendLine(SmartBpBusinessStateFormatter.Format(result, resolver, includeResolved: true));
        AppendResolved(result.BannedSur, Camp.Sur, resolved);
        AppendResolved(result.BannedHun, Camp.Hun, resolved);
        AppendResolved(result.PickedSur, Camp.Sur, resolved, includePlayer: true);
        AppendResolved([result.PickedHun], Camp.Hun, resolved, includePlayer: true, hunterSlot: true);
        return (visual.ToString().TrimEnd(), resolved.ToString().TrimEnd());
    }
    private void AppendResolved(IEnumerable<SmartBpRecognizedCharacterSlot> slots, Camp camp, StringBuilder builder, bool includePlayer = false, bool hunterSlot = false)
    {
        foreach (var slot in slots)
        {
            if (SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)) continue;
            var index = hunterSlot ? -1 : slot.Index;
            var match = resolver.Resolve(slot.CharacterName, camp, index, 1);
            var playerId = includePlayer && slot is SmartBpRecognizedPlayerCharacterSlot player ? player.PlayerId : null;
            builder.AppendLine($"{camp}[{index}] raw={slot.CharacterName}; resolved={match.ResolvedCharacterName ?? "unresolved"}; playerId={playerId ?? "null"}{(match.Warnings.Count > 0 ? "; " + string.Join("; ", match.Warnings) : "")}");
        }
    }
    private static void Validate(int slot, double confidence) { if (slot is < 0 or > 15) throw new InvalidDataException("Invalid slot index."); if (confidence is < 0 or > 1) throw new InvalidDataException("Invalid confidence."); }
    private static void ValidateSide(string value) { if (value is not ("left" or "right" or "top" or "bottom" or "unknown")) throw new InvalidDataException("Invalid side."); }
    private static void ValidateState(string value) { if (value is not ("selected" or "waiting" or "unselected" or "banned" or "unknown")) throw new InvalidDataException("Invalid slot_state."); }
    private static Camp? ParseFaction(string value) => value switch { "survivor" => Camp.Sur, "hunter" => Camp.Hun, "unknown" => null, _ => throw new InvalidDataException("Invalid faction.") };
}

internal sealed class LlamaCppRequestException(string message, string rawResponse) : Exception(message)
{
    public string RawResponse { get; } = rawResponse;
}
