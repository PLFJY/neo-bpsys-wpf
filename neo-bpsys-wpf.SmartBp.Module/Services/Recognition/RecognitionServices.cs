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

internal sealed class SmartBpAiOcrTranscriptRecognitionService(
    ISmartBpImageEncoder encoder,
    ISmartBpRecognitionFrameCropper cropper,
    ISmartBpRecognitionSettingsService settings,
    ILlamaCppServerManagerFactory serverManagers,
    ISmartBpDebugLog debugLog) : ISmartBpAiOcrTranscriptRecognitionService
{
    private const string Prompt = """
Read visible text in the cropped Identity V UI image.
Do not infer missing text.
Do not explain.
Return only JSON:
{"lines":[{"text":"..."}]}
""";

    public async Task<SmartBpAiOcrTranscriptResult> RecognizeAsync(
        BitmapSource frame,
        IReadOnlyList<(SmartBpRecognitionRegion Region, string Field)> regions,
        CancellationToken cancellationToken = default)
    {
        var allLines = new List<SmartBpAiOcrTranscriptLine>();
        var diagnostics = new List<string>();
        var rawBuilder = new StringBuilder();
        foreach (var (region, field) in regions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var crop = await Task.Run(() => cropper.CropWithInfo(frame, region), cancellationToken).ConfigureAwait(false);
            var imageDataUrl = await Task.Run(
                () => encoder.EncodeDataUrl(crop.Image, Math.Max(256, settings.Settings.ContentCropMaxImageWidth)),
                cancellationToken).ConfigureAwait(false);
            var raw = await RecognizeRegionAsync(imageDataUrl, region, field, cancellationToken).ConfigureAwait(false);
            rawBuilder.AppendLine($"[{ToRegionId(region)} field={field}]").AppendLine(raw);
            var lines = ParseLines(raw);
            diagnostics.Add($"AI OCR transcript region={ToRegionId(region)}; field={field}; line_count={lines.Count}.");
            allLines.AddRange(lines);
        }

        return new()
        {
            Lines = allLines,
            RawJson = rawBuilder.ToString().TrimEnd(),
            Diagnostics = diagnostics
        };
    }

    private async Task<string> RecognizeRegionAsync(
        string imageDataUrl,
        SmartBpRecognitionRegion region,
        string field,
        CancellationToken cancellationToken)
    {
        var role = ShouldReuseBusinessServer() ? LlamaVisionServerRole.BusinessAi : LlamaVisionServerRole.AiOcr;
        var port = serverManagers.Get(role).Port;
        var body = new JsonObject
        {
            ["model"] = "local",
            ["temperature"] = 0,
            ["max_tokens"] = Math.Max(128, settings.Settings.SnapshotDeltaMaxTokens),
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = "You are a precise OCR text extractor." },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray(
                        new JsonObject { ["type"] = "text", ["text"] = Prompt },
                        new JsonObject { ["type"] = "text", ["text"] = $"region={ToRegionId(region)}; field={field}" },
                        new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = imageDataUrl } })
                })
        };
        var url = $"http://127.0.0.1:{port}/v1/chat/completions";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(settings.Settings.AiRequestTimeoutSeconds) };
        debugLog.Write("recognition", $"POST {url}; task=AiOcrTranscript; region={ToRegionId(region)}; field={field}; role={role}");
        using var response = await http.PostAsync(url, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
        var envelope = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new LlamaCppRequestException($"llama.cpp returned {(int)response.StatusCode}: {envelope}", envelope);
        using var document = JsonDocument.Parse(envelope);
        var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content))
            throw new LlamaCppRequestException("llama.cpp returned empty AI OCR content.", envelope);
        var (repaired, _) = SmartBpJsonRepair.Repair(content);
        debugLog.Write("recognition", $"AI OCR transcript raw region={ToRegionId(region)}:\n{content}");
        return repaired;
    }

    private bool ShouldReuseBusinessServer() =>
        !settings.Settings.UseSeparateAiOcrServer ||
        string.Equals(settings.Settings.SelectedBusinessAiModelId, settings.Settings.SelectedAiOcrModelId, StringComparison.Ordinal);

    private static IReadOnlyList<SmartBpAiOcrTranscriptLine> ParseLines(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        if (!document.RootElement.TryGetProperty("lines", out var linesElement) ||
            linesElement.ValueKind != JsonValueKind.Array)
            return [];
        return linesElement.EnumerateArray()
            .Select(item => item.TryGetProperty("text", out var text) ? text.GetString() : null)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => new SmartBpAiOcrTranscriptLine { Text = text! })
            .ToArray();
    }

    private static string ToRegionId(SmartBpRecognitionRegion region) =>
        region switch
        {
            SmartBpRecognitionRegion.PhaseTop => "phase_top",
            SmartBpRecognitionRegion.LeftTop => "left_top",
            SmartBpRecognitionRegion.RightTop => "right_top",
            SmartBpRecognitionRegion.LeftBottom => "left_bottom",
            SmartBpRecognitionRegion.RightBottom => "right_bottom",
            _ => region.ToString()
        };
}

internal sealed class SmartBpAiOcrTranscriptInterpreter(ICharacterSelectionService characterSelection) : ISmartBpAiOcrTranscriptInterpreter
{
    public (SmartBpSnapshotFieldUpdate Update, IReadOnlyList<string> Diagnostics) Interpret(
        SmartBpAiOcrTranscriptResult transcript,
        SmartBpRecognitionRegion region,
        string field)
    {
        var diagnostics = new List<string> { $"AI OCR transcript interpreter region={ToRegionId(region)}; field={field}; line_count={transcript.Lines.Count}." };
        return field switch
        {
            "banned_sur" => (new() { Field = field, Slots = InterpretCharacterSlots(transcript, Camp.Sur, 4, diagnostics) }, diagnostics),
            "banned_hun" => (new() { Field = field, Slots = InterpretCharacterSlots(transcript, Camp.Hun, 2, diagnostics) }, diagnostics),
            "picked_sur" => (new() { Field = field, Slots = InterpretPlayerSlots(transcript, Camp.Sur, 4, diagnostics) }, diagnostics),
            "picked_hun" => (new() { Field = field, PickedHun = InterpretPickedHunter(transcript, diagnostics) }, diagnostics),
            _ => (new() { Field = field }, [$"AI OCR transcript interpreter skipped unsupported field={field}."])
        };
    }

    private List<SmartBpSnapshotDeltaSlot> InterpretCharacterSlots(
        SmartBpAiOcrTranscriptResult transcript,
        Camp camp,
        int count,
        ICollection<string> diagnostics)
    {
        var matches = ResolveCharacters(transcript, camp, diagnostics).Take(count).ToArray();
        var slots = DefaultSlots(count);
        for (var i = 0; i < matches.Length; i++)
        {
            slots[i].SlotState = "selected";
            slots[i].CharacterName = matches[i].CanonicalName ?? matches[i].CharacterKey ?? "未选择";
        }

        return slots;
    }

    private List<SmartBpSnapshotDeltaSlot> InterpretPlayerSlots(
        SmartBpAiOcrTranscriptResult transcript,
        Camp camp,
        int count,
        ICollection<string> diagnostics)
    {
        var lines = transcript.Lines.Select(line => line.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
        var characterIndexes = new HashSet<int>();
        var slots = DefaultSlots(count);
        var slot = 0;
        for (var i = 0; i < lines.Length && slot < count; i++)
        {
            var resolved = Resolve(lines[i], camp, diagnostics);
            if (resolved.Character == null)
                continue;
            slots[slot].SlotState = "selected";
            slots[slot].CharacterName = resolved.CanonicalName ?? resolved.CharacterKey ?? "未选择";
            characterIndexes.Add(i);
            var playerId = lines.Skip(i + 1)
                .Where((_, offset) => !characterIndexes.Contains(i + 1 + offset))
                .FirstOrDefault(candidate => Resolve(candidate, camp, diagnostics, logUnresolved: false).Character == null);
            if (!string.IsNullOrWhiteSpace(playerId))
                slots[slot].PlayerId = playerId.Trim();
            slot++;
        }

        return slots;
    }

    private SmartBpSnapshotDeltaSlot InterpretPickedHunter(
        SmartBpAiOcrTranscriptResult transcript,
        ICollection<string> diagnostics)
    {
        var lines = transcript.Lines.Select(line => line.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
        var result = new SmartBpSnapshotDeltaSlot { Index = 0, SlotState = "unknown", CharacterName = "未选择" };
        for (var i = 0; i < lines.Length; i++)
        {
            var resolved = Resolve(lines[i], Camp.Hun, diagnostics);
            if (resolved.Character == null)
                continue;
            result.SlotState = "selected";
            result.CharacterName = resolved.CanonicalName ?? resolved.CharacterKey ?? "未选择";
            result.PlayerId = lines.Skip(i + 1).FirstOrDefault(candidate => Resolve(candidate, Camp.Hun, diagnostics, logUnresolved: false).Character == null)?.Trim();
            return result;
        }

        return result;
    }

    private IEnumerable<CharacterResolveResult> ResolveCharacters(
        SmartBpAiOcrTranscriptResult transcript,
        Camp camp,
        ICollection<string> diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in transcript.Lines)
        {
            var resolved = Resolve(line.Text, camp, diagnostics);
            var key = resolved.CanonicalName ?? resolved.CharacterKey;
            if (resolved.Character == null || key == null || !seen.Add(key))
                continue;
            yield return resolved;
        }
    }

    private CharacterResolveResult Resolve(
        string text,
        Camp camp,
        ICollection<string> diagnostics,
        bool logUnresolved = true)
    {
        var resolved = characterSelection.ResolveCharacterDetailed(text, camp);
        if (resolved.Character != null)
            diagnostics.Add($"ai-ocr-match raw={text}; camp={camp}; result={resolved.CanonicalName}; matchMode={resolved.MatchMode}; score={resolved.Score:0.00}; safe={resolved.IsAutoApplySafe}; reason={resolved.Reason}");
        else if (logUnresolved)
            diagnostics.Add($"ai-ocr-unresolved raw={text}; camp={camp}; matchMode={resolved.MatchMode}; score={resolved.Score:0.00}; reason={resolved.Reason}");
        return resolved;
    }

    private static List<SmartBpSnapshotDeltaSlot> DefaultSlots(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new SmartBpSnapshotDeltaSlot { Index = index, SlotState = "unknown", CharacterName = "未选择" })
            .ToList();

    private static string ToRegionId(SmartBpRecognitionRegion region) =>
        region switch
        {
            SmartBpRecognitionRegion.PhaseTop => "phase_top",
            SmartBpRecognitionRegion.LeftTop => "left_top",
            SmartBpRecognitionRegion.RightTop => "right_top",
            SmartBpRecognitionRegion.LeftBottom => "left_bottom",
            SmartBpRecognitionRegion.RightBottom => "right_bottom",
            _ => region.ToString()
        };
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

internal static class SmartBpRecognitionPromptBuilder
{
    public static string BuildSnapshotDelta(
        SmartBpSnapshotDeltaRequest request,
        IEnumerable<string> survivors,
        IEnumerable<string> hunters,
        bool includeCandidateLists = true)
    {
        var fields = request.RequestedFields.ToArray();
        var mapping = string.Join(Environment.NewLine, request.RequestedRegions.Select((item, index) =>
            $"image_{index + 1} = {RegionId(item.Region)}, field={item.TargetField}"));
        var currentKnownState = FormatCurrentKnownState(request.CurrentKnownState);
        var candidatePolicy = includeCandidateLists
            ? $"survivor_candidates: {JsonSerializer.Serialize(survivors)}{Environment.NewLine}hunter_candidates: {JsonSerializer.Serialize(hunters)}{Environment.NewLine}character_name 必须是对应候选列表中的规范名称，或 \"未选择\"。"
            : "candidate_lists: omitted. Output the visible character name text or \"未选择\".";
        return $$$"""
/no_think

你会收到多张第五人格 BP 裁剪图。必须遵守严格 multi-image protocol。

image_0 = phase_top
  Only determine phase.
  Never output characters from phase_top.

Each additional image has an explicit id and field:
  right_top -> banned_sur
  left_top -> banned_hun
  left_bottom -> picked_sur
  right_bottom -> picked_hun

{{{mapping}}}

只输出一个 JSON：
{
  "phase": "...",
  "updates": [...]
}

requested_fields: {{{JsonSerializer.Serialize(fields)}}}
{{{candidatePolicy}}}
current_known_state: {{{currentKnownState}}}

phase 只能是：
["大厅","规则设置","查看禁选顺序","选择禁用数量","开始案件还原","阵容选择中","屏蔽求生者","屏蔽监管者","选择求生者","求生者选择角色中","选择监管者","求生者选择天赋中","监管者选择天赋中","天赋已锁定","即将进入区域选择","区域选择","求生者选择区域中","监管者选择区域中","等待游戏开始","加载中","对局中","等待中","未知"]

全局规则：
- Only output updates for requested_fields.
- Never output a field whose crop was not provided.
- Never infer a field from phase alone.
- Never infer one crop's field from another crop.
- Each crop has exactly one responsibility.
- 如果 requested field 的裁剪图里仍可见上一阶段结果，即使当前 phase 已进入下一步，也必须输出该区域当前可见业务状态。
- 每个 slot 必须输出 slot_state："selected"、"empty"、"unknown"。

slot_state 含义：
- selected: The crop clearly shows a character in this slot. character_name must be a candidate character.
- empty: The crop clearly shows an empty/unselected slot. character_name must be "未选择".
- unknown: The crop does not provide enough reliable evidence for this slot. Local merge must preserve the previous known value.
- unknown is not an error. It is a state-preserving result.
- Do not use empty for dark/disabled/crossed old ban slots.

current_known_state 使用规则：
- Use current_known_state only to decide whether an unreadable old slot should be "unknown" rather than "empty".
- Do not invent new characters from current_known_state.
- If the crop clearly shows a different selected character, output selected with the visible character.
- If the crop clearly shows empty/未选择, output empty.
- If the crop is unreadable but current_known_state has a previously known selected character, output unknown to preserve it.
- Do not let current_known_state override visible crop evidence.

field responsibility:
right_top is the hunter-side survivor-ban area.
It outputs banned_sur only.
banned_sur has exactly 4 slots: index 0,1,2,3.
Slot order is visual left-to-right.
Index 0 and 1 are usually first-round bans.
Index 2 may be a later-round ban.
Index 3 may be a later-round ban.
Already banned old slots may appear dark, semi-transparent, crossed, disabled, or with a red ban icon.
Those visual effects mean selected/banned, not empty.
Only output empty/未选择 if the slot is clearly empty or clearly says 未选择.
Do not output only the currently active ban slot; output the visible state of all four banned_sur slots.

left_top is the survivor-side hunter-ban area.
It outputs banned_hun only.
banned_hun has exactly 2 slots: index 0,1.
Slot order is visual left-to-right.
Dark/disabled/red-ban old slots are still selected bans, not empty.

left_bottom is the survivor pick/distribution/talent visible area.
It outputs picked_sur only.
picked_sur has exactly 4 slots: index 0,1,2,3.
Slot order is visual left-to-right by player slot.
Character name is usually the highest text under/near the character portrait.
Player id is usually below character name.
Talent name may appear below player id; do not put talent name into character_name.
If the phase is survivor talent, keep visible selected survivor characters if readable.

right_bottom is the hunter pick/talent visible area.
It outputs picked_hun only.
picked_hun has one visual slot, index 0.
Character name is usually the first/highest line under the hunter portrait.
Player id is usually the second line.
Talent name may appear lower; do not put talent name into character_name.

Examples:
BanSur second round:
right_top crop:
- slot 0 shows 小说家, dimmed with ban mark
- slot 1 shows 昆虫学者, dimmed with ban mark
- slot 2 shows 入殓师
- slot 3 shows 未选择
Output:
banned_sur[0] slot_state=selected character_name=小说家
banned_sur[1] slot_state=selected character_name=昆虫学者
banned_sur[2] slot_state=selected character_name=入殓师
banned_sur[3] slot_state=empty character_name=未选择

BanSur third round:
right_top crop:
- slot 0 shows 小说家
- slot 1 shows 昆虫学者
- slot 2 shows 入殓师
- slot 3 shows 祭司
Output all four selected slots.

Unreadable old slot:
right_top crop:
- slot 0 is too dark/unreadable
- current_known_state.banned_sur[0] = 小说家
- slot 2 clearly shows 入殓师
Output:
slot 0 slot_state=unknown character_name=未选择
slot 2 slot_state=selected character_name=入殓师
The local merge will preserve slot 0 as 小说家.

不要输出完整 BP 快照，除非所有字段都被请求。
不要输出 teams、all_characters、all_player_ids、scene、warnings、raw_visible_text、confidence、MapBP 字段。
""";
    }

    private static string FormatCurrentKnownState(SmartBpBusinessStateRecognitionResult? state)
    {
        return CreateCurrentKnownStateJson(state).ToJsonString();
    }

    internal static string FormatCurrentKnownStateForDiagnostics(SmartBpBusinessStateRecognitionResult? state)
    {
        return CreateCurrentKnownStateJson(state).ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static JsonObject CreateCurrentKnownStateJson(SmartBpBusinessStateRecognitionResult? state)
    {
        static string[] Names(IEnumerable<SmartBpRecognizedCharacterSlot> slots, int count) =>
            slots.OrderBy(x => x.Index).Take(count).Select(x => string.IsNullOrWhiteSpace(x.CharacterName) ? "未选择" : x.CharacterName).ToArray();
        return state == null
            ? new JsonObject
            {
                ["banned_sur"] = new JsonArray("未选择", "未选择", "未选择", "未选择"),
                ["banned_hun"] = new JsonArray("未选择", "未选择"),
                ["picked_sur"] = new JsonArray("未选择", "未选择", "未选择", "未选择"),
                ["picked_hun"] = "未选择"
            }
            : new JsonObject
            {
                ["banned_sur"] = new JsonArray(Names(state.BannedSur, 4).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                ["banned_hun"] = new JsonArray(Names(state.BannedHun, 2).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                ["picked_sur"] = new JsonArray(Names(state.PickedSur, 4).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                ["picked_hun"] = string.IsNullOrWhiteSpace(state.PickedHun.CharacterName) ? "未选择" : state.PickedHun.CharacterName
            };
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
["大厅","规则设置","查看禁选顺序","选择禁用数量","开始案件还原","阵容选择中","屏蔽求生者","屏蔽监管者","选择求生者","求生者选择角色中","选择监管者","求生者选择天赋中","监管者选择天赋中","天赋已锁定","即将进入区域选择","区域选择","求生者选择区域中","监管者选择区域中","等待游戏开始","加载中","对局中","等待中","未知"]
非活动侧的“等待中”不能决定 phase。
如果右上标题是“屏蔽求生者”，phase="屏蔽求生者"。
如果左上标题是“屏蔽监管者”，phase="屏蔽监管者"。
如果左侧/求生者方标题包含“选择天赋中”，phase="求生者选择天赋中"。
如果右侧/监管者方标题包含“选择天赋中”，phase="监管者选择天赋中"。
不要输出地图 BP。
区域选择只用于输出对应 phase 以停止角色 BP，不识别区域或地图内容。
""";

    public static string BuildPhaseOnly() => """
/no_think

你只会收到第五人格 BP 顶部/阶段裁剪图。
只判断当前 phase。
只输出 JSON：
{"phase":"..."}
""";

    public static string BuildBannedSurFieldSnapshot() => """
/no_think

你只会收到 right_top 裁剪图。
这是监管者方禁用求生者区域。
只输出 banned_sur。
banned_sur 固定 4 个槽，index 0,1,2,3。
槽位顺序按视觉从左到右。
已禁用旧槽可能变暗、半透明、红叉、禁用标记；这些都表示 selected/banned，不是 empty。
只有明确显示“未选择”或空槽，slot_state 才能是 empty。
看不清但不能确定为空时，slot_state 输出 unknown。
不要输出 phase。
不要输出 picked_sur / picked_hun / banned_hun。
只输出 JSON：
{"field":"banned_sur","slots":[{"index":0,"slot_state":"selected","character_name":"..."},{"index":1,"slot_state":"selected","character_name":"..."},{"index":2,"slot_state":"selected","character_name":"..."},{"index":3,"slot_state":"empty","character_name":"未选择"}]}
""";

    public static string BuildBannedHunFieldSnapshot() => """
/no_think

你只会收到 left_top 裁剪图。
这是求生者方禁用监管者区域。
只输出 banned_hun。
banned_hun 固定 2 个槽，index 0,1。
槽位顺序按视觉从左到右。
已禁用旧槽可能变暗、半透明、红叉、禁用标记；这些都表示 selected/banned，不是 empty。
只有明确显示“未选择”或空槽，slot_state 才能是 empty。
看不清但不能确定为空时，slot_state 输出 unknown。
不要输出 phase。
不要输出 banned_sur / picked_sur / picked_hun。
只输出 JSON：
{"field":"banned_hun","slots":[{"index":0,"slot_state":"selected","character_name":"..."},{"index":1,"slot_state":"empty","character_name":"未选择"}]}
""";

    public static string BuildPickedSurFieldSnapshot() => """
/no_think

你只会收到 left_bottom 裁剪图。
这是求生者选择/角色分配区域。
只输出 picked_sur。
4 survivor player slots left-to-right.
character_name is usually the highest text near/under portrait.
player_id is below character_name.
talent name may appear below player_id; do not put talent into character_name.
picked_sur 固定 4 个槽，index 0,1,2,3。
只有明确显示“未选择”或空槽，slot_state 才能是 empty。
看不清但不能确定为空时，slot_state 输出 unknown。
不要输出 phase。
不要输出 banned_sur / banned_hun / picked_hun。
只输出 JSON：
{"field":"picked_sur","slots":[{"index":0,"slot_state":"selected","character_name":"...","player_id":"..."},{"index":1,"slot_state":"selected","character_name":"...","player_id":"..."},{"index":2,"slot_state":"empty","character_name":"未选择","player_id":null},{"index":3,"slot_state":"empty","character_name":"未选择","player_id":null}]}
""";

    public static string BuildPickedHunFieldSnapshot() => """
/no_think

你只会收到 right_bottom 裁剪图。
这是监管者选择区域。
只输出 picked_hun。
picked_hun 只有一个槽，index 0。
character_name 通常是监管者头像下方第一行。
player_id 通常是第二行。
talent name may appear lower; do not put talent name into character_name。
只有明确显示“未选择”或空槽，slot_state 才能是 empty。
看不清但不能确定为空时，slot_state 输出 unknown。
不要输出 phase。
不要输出 banned_sur / banned_hun / picked_sur。
只输出 JSON：
{"field":"picked_hun","picked_hun":{"index":0,"slot_state":"selected","character_name":"...","player_id":"..."}}
""";

    /// <summary>Returns the field-snapshot user prompt for the requested field id.</summary>
    public static string BuildFieldSnapshot(string field) => field switch
    {
        "banned_sur" => BuildBannedSurFieldSnapshot(),
        "banned_hun" => BuildBannedHunFieldSnapshot(),
        "picked_sur" => BuildPickedSurFieldSnapshot(),
        "picked_hun" => BuildPickedHunFieldSnapshot(),
        _ => throw new NotSupportedException($"Field snapshot prompt does not support field '{field}'.")
    };

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
["大厅","规则设置","查看禁选顺序","选择禁用数量","开始案件还原","阵容选择中","屏蔽求生者","屏蔽监管者","选择求生者","求生者选择角色中","选择监管者","求生者选择天赋中","监管者选择天赋中","天赋已锁定","即将进入区域选择","区域选择","求生者选择区域中","监管者选择区域中","等待游戏开始","加载中","对局中","等待中","未知"]

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

    public static JsonObject GetBannedSurFieldSnapshot(IReadOnlyList<string> survivorCandidates, bool strictCandidateEnums)
    {
        var characterName = strictCandidateEnums ? CharacterNameEnum(survivorCandidates) : StringCharacterName();
        return Object(new JsonObject
        {
            ["field"] = Const("banned_sur"),
            ["slots"] = FixedArray(DeltaSlot(characterName, 0, 1, 2, 3), 4)
        }, "field", "slots");
    }

    public static JsonObject GetBannedHunFieldSnapshot(IReadOnlyList<string> hunterCandidates, bool strictCandidateEnums)
    {
        var characterName = strictCandidateEnums ? CharacterNameEnum(hunterCandidates) : StringCharacterName();
        return Object(new JsonObject
        {
            ["field"] = Const("banned_hun"),
            ["slots"] = FixedArray(DeltaSlot(characterName, 0, 1), 2)
        }, "field", "slots");
    }

    public static JsonObject GetPickedSurFieldSnapshot(IReadOnlyList<string> survivorCandidates, bool strictCandidateEnums)
    {
        var characterName = strictCandidateEnums ? CharacterNameEnum(survivorCandidates) : StringCharacterName();
        return Object(new JsonObject
        {
            ["field"] = Const("picked_sur"),
            ["slots"] = FixedArray(DeltaSlot(characterName, 0, 1, 2, 3), 4)
        }, "field", "slots");
    }

    public static JsonObject GetPickedHunFieldSnapshot(IReadOnlyList<string> hunterCandidates, bool strictCandidateEnums)
    {
        var characterName = strictCandidateEnums ? CharacterNameEnum(hunterCandidates) : StringCharacterName();
        return Object(new JsonObject
        {
            ["field"] = Const("picked_hun"),
            ["picked_hun"] = Object(new JsonObject { ["index"] = Const(0), ["slot_state"] = DeltaSlotState(), ["character_name"] = characterName, ["player_id"] = NullableString() }, "index", "slot_state", "character_name", "player_id")
        }, "field", "picked_hun");
    }

    /// <summary>Returns the field-snapshot JSON schema for the requested field id.</summary>
    public static JsonObject GetFieldSnapshot(string field, IReadOnlyList<string> survivorCandidates, IReadOnlyList<string> hunterCandidates, bool strictCandidateEnums) => field switch
    {
        "banned_sur" => GetBannedSurFieldSnapshot(survivorCandidates, strictCandidateEnums),
        "banned_hun" => GetBannedHunFieldSnapshot(hunterCandidates, strictCandidateEnums),
        "picked_sur" => GetPickedSurFieldSnapshot(survivorCandidates, strictCandidateEnums),
        "picked_hun" => GetPickedHunFieldSnapshot(hunterCandidates, strictCandidateEnums),
        _ => throw new NotSupportedException($"Field snapshot schema does not support field '{field}'.")
    };

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
        var updateShapes = new JsonArray();
        if (requestedFields.Contains("banned_sur"))
            updateShapes.Add(Object(new JsonObject
            {
                ["field"] = Const("banned_sur"),
                ["slots"] = FixedArray(DeltaSlot(survivorNames, 0, 1, 2, 3), 4),
                ["picked_hun"] = new JsonObject { ["type"] = "null" }
            }, "field", "slots", "picked_hun"));
        if (requestedFields.Contains("banned_hun"))
            updateShapes.Add(Object(new JsonObject
            {
                ["field"] = Const("banned_hun"),
                ["slots"] = FixedArray(DeltaSlot(hunterNames, 0, 1), 2),
                ["picked_hun"] = new JsonObject { ["type"] = "null" }
            }, "field", "slots", "picked_hun"));
        if (requestedFields.Contains("picked_sur"))
            updateShapes.Add(Object(new JsonObject
            {
                ["field"] = Const("picked_sur"),
                ["slots"] = FixedArray(DeltaSlot(survivorNames, 0, 1, 2, 3), 4),
                ["picked_hun"] = new JsonObject { ["type"] = "null" }
            }, "field", "slots", "picked_hun"));
        if (requestedFields.Contains("picked_hun"))
            updateShapes.Add(Object(new JsonObject
            {
                ["field"] = Const("picked_hun"),
                ["slots"] = new JsonObject { ["type"] = "null" },
                ["picked_hun"] = DeltaSlot(hunterNames, 0)
            }, "field", "slots", "picked_hun"));
        JsonObject update = new() { ["oneOf"] = updateShapes };
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
    private static JsonObject DeltaSlot(JsonObject characterName, params int[] indexes) => Object(new JsonObject { ["index"] = IntegerEnum(indexes), ["slot_state"] = DeltaSlotState(), ["character_name"] = characterName.DeepClone(), ["player_id"] = NullableString() }, "index", "slot_state", "character_name", "player_id");
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
    private static JsonObject Phase() => new() { ["type"] = "string", ["enum"] = new JsonArray(
        "大厅", "规则设置", "查看禁选顺序", "选择禁用数量", "开始案件还原", "阵容选择中",
        "屏蔽求生者", "屏蔽监管者", "选择求生者", "求生者选择角色中", "选择监管者",
        "求生者选择天赋中", "监管者选择天赋中", "天赋已锁定", "即将进入区域选择", "区域选择",
        "求生者选择区域中", "监管者选择区域中", "等待游戏开始", "加载中", "对局中", "等待中", "未知") };
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
    private static JsonObject DeltaSlotState() => new() { ["type"] = "string", ["enum"] = new JsonArray("selected", "empty", "unknown") };
    private static JsonObject StringArray() => new() { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } };
    private static JsonObject Array(JsonNode? item) => new() { ["type"] = "array", ["items"] = item };
}

internal static class SmartBpJsonRepair
{
    private static readonly Regex FenceRegex = new(@"^\s*```(?:json)?\s*\n?([\s\S]*?)\n?\s*```\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FirstObjectRegex = new(@"\{[\s\S]*\}", RegexOptions.Compiled);

    /// <summary>Repairs common model output issues such as Markdown JSON fences and surrounding prose.</summary>
    /// <param name="raw">Raw model content.</param>
    /// <returns>The repaired JSON string and whether a fence was removed.</returns>
    public static (string Repaired, bool RemovedFence) Repair(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (raw, false);
        var trimmed = raw.Trim();
        var match = FenceRegex.Match(trimmed);
        if (match.Success)
            return (match.Groups[1].Value.Trim(), true);
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var withoutOpening = trimmed[3..];
            if (withoutOpening.StartsWith("json", StringComparison.OrdinalIgnoreCase)) withoutOpening = withoutOpening[4..];
            withoutOpening = withoutOpening.TrimStart('\r', '\n', ' ', '\t');
            if (withoutOpening.EndsWith("```", StringComparison.Ordinal)) withoutOpening = withoutOpening[..^3];
            return (withoutOpening.Trim(), true);
        }
        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}'))
        {
            var objectMatch = FirstObjectRegex.Match(trimmed);
            if (objectMatch.Success) return (objectMatch.Value, false);
        }
        return (trimmed, false);
    }
}

internal sealed class LlamaCppOpenAiClient(ISmartBpRecognitionSettingsService settings, ISharedDataService shared, ISmartBpPromptProfileProvider promptProfiles, ILogger<LlamaCppOpenAiClient> logger, ISmartBpDebugLog debugLog) : ILlamaCppOpenAiClient
{
    public LlamaCppResponseMetrics? LastResponseMetrics { get; private set; }
    public string? LastFinishReason { get; private set; }
    public async Task<string> RecognizeSnapshotDeltaAsync(IReadOnlyList<SmartBpMultimodalRegionInput> regions, SmartBpSnapshotDeltaRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await promptProfiles.LoadAsync(settings.Settings.PromptProfileId, cancellationToken);
        var needsSurvivors = request.RequestedFields.Any(field => field is "banned_sur" or "picked_sur");
        var needsHunters = request.RequestedFields.Any(field => field is "banned_hun" or "picked_hun");
        var survivorCandidates = needsSurvivors ? shared.SurCharaDict.Keys : Enumerable.Empty<string>();
        var hunterCandidates = needsHunters ? shared.HunCharaDict.Keys : Enumerable.Empty<string>();
        var includeCandidateLists = settings.Settings.UseStrictCandidateEnumsInAutoSchema;
        var prompt = SmartBpRecognitionPromptBuilder.BuildSnapshotDelta(request, survivorCandidates, hunterCandidates, includeCandidateLists);
        var imageLabels = regions.Select((region, index) => $"image_{index}={region.Id}/field={region.TargetField}").ToArray();
        debugLog.Write("recognition", $"Snapshot delta requested_fields=[{string.Join(", ", request.RequestedFields)}]; image_labels=[{string.Join("; ", imageLabels)}].");
        debugLog.Write("recognition", $"Snapshot delta candidate_lists={(includeCandidateLists ? "included" : "omitted")}; prompt_chars={prompt.Length}.");
        debugLog.Write("recognition", $"Snapshot delta current_known_state={SmartBpRecognitionPromptBuilder.FormatCurrentKnownStateForDiagnostics(request.CurrentKnownState)}");
        debugLog.Write("recognition", $"Snapshot delta prompt text:\n{prompt}");
        var content = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = prompt } };
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
        var raw = await SendSpecialAsync(body, $"SnapshotDelta:{string.Join(",", request.RequestedFields)}", cancellationToken);
        debugLog.Write("recognition", $"Snapshot delta raw JSON response:\n{raw}");
        return raw;
    }

    public async Task<string> RecognizePhaseAsync(string imageDataUrl, CancellationToken cancellationToken = default)
    {
        var profile = await promptProfiles.LoadAsync(settings.Settings.PromptProfileId, cancellationToken);
        var body = CreateBody(profile.SystemPrompt, SmartBpRecognitionPromptBuilder.BuildPhaseRecognition(), imageDataUrl,
            SmartBpRecognitionJsonSchemaProvider.GetPhaseOnly(), settings.Settings.PhaseMaxTokens);
        return await SendSpecialAsync(body, "PhaseTop", cancellationToken);
    }

    public async Task<string> RecognizePhaseOnlyAsync(string imageDataUrl, CancellationToken cancellationToken = default)
    {
        var profile = await promptProfiles.LoadAsync(settings.Settings.PromptProfileId, cancellationToken);
        var prompt = SmartBpRecognitionPromptBuilder.BuildPhaseOnly();
        var mode = settings.Settings.StructuredOutputMode;
        debugLog.Write("recognition", $"Phase-only path; structured_output_mode={mode}; prompt_chars={prompt.Length}; max_tokens={settings.Settings.PhaseMaxTokens}.");
        debugLog.Write("recognition", $"Phase-only prompt text:\n{prompt}");
        var body = CreateStructuredBody(profile.SystemPrompt, prompt, imageDataUrl,
            SmartBpRecognitionJsonSchemaProvider.GetPhaseOnly(), settings.Settings.PhaseMaxTokens, mode, "smartbp_phase");
        var raw = await SendSpecialAsync(body, "PhaseOnly", cancellationToken);
        return await RepairAndLogAsync(raw, mode, "PhaseOnly");
    }

    public async Task<string> RecognizeFieldSnapshotAsync(string imageDataUrl, string field, CancellationToken cancellationToken = default)
    {
        var profile = await promptProfiles.LoadAsync(settings.Settings.PromptProfileId, cancellationToken);
        var prompt = SmartBpRecognitionPromptBuilder.BuildFieldSnapshot(field);
        var maxTokens = FieldMaxTokens(field);
        var mode = settings.Settings.StructuredOutputMode;
        var strictCandidateEnums = settings.Settings.UseStrictCandidateEnumsInAutoSchema;
        var schema = SmartBpRecognitionJsonSchemaProvider.GetFieldSnapshot(field, shared.SurCharaDict.Keys.ToArray(), shared.HunCharaDict.Keys.ToArray(), strictCandidateEnums);
        debugLog.Write("recognition", $"Field snapshot field={field}; structured_output_mode={mode}; prompt_chars={prompt.Length}; max_tokens={maxTokens}.");
        debugLog.Write("recognition", $"Field snapshot prompt text:\n{prompt}");
        var body = CreateStructuredBody(profile.SystemPrompt, prompt, imageDataUrl, schema, maxTokens, mode, $"smartbp_field_{field}");
        var raw = await SendSpecialAsync(body, $"FieldSnapshot:{field}", cancellationToken);
        return await RepairAndLogAsync(raw, mode, $"FieldSnapshot:{field}");
    }

    private int FieldMaxTokens(string field) => field switch
    {
        "banned_sur" => settings.Settings.BannedSurFieldMaxTokens,
        "banned_hun" => settings.Settings.BannedHunFieldMaxTokens,
        "picked_sur" => settings.Settings.PickedSurFieldMaxTokens,
        "picked_hun" => settings.Settings.PickedHunFieldMaxTokens,
        _ => settings.Settings.SnapshotDeltaMaxTokens
    };

    private async Task<string> RepairAndLogAsync(string raw, AiStructuredOutputMode mode, string taskLabel)
    {
        if (mode != AiStructuredOutputMode.JsonPromptAndRepair)
        {
            debugLog.Write("recognition", $"{taskLabel} raw JSON response:\n{raw}");
            return raw;
        }
        var (repaired, removedFence) = SmartBpJsonRepair.Repair(raw);
        if (removedFence) debugLog.Write("recognition", $"Removed markdown JSON fence during repair for {taskLabel}.");
        debugLog.Write("recognition", $"{taskLabel} raw JSON response:\n{raw}");
        if (!string.Equals(repaired, raw, StringComparison.Ordinal))
            debugLog.Write("recognition", $"{taskLabel} repaired JSON:\n{repaired}");
        return repaired;
    }

    private JsonObject CreateStructuredBody(string systemPrompt, string userPrompt, string imageDataUrl, JsonObject schema, int maxTokens, AiStructuredOutputMode mode, string schemaName)
    {
        var content = new JsonArray(
            new JsonObject { ["type"] = "text", ["text"] = userPrompt },
            new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = imageDataUrl } });
        if (mode == AiStructuredOutputMode.JsonPromptAndRepair)
        {
            content.Insert(0, new JsonObject { ["type"] = "text", ["text"] = "只输出 JSON。\n不要输出 Markdown。\n不要输出 ```json 代码块。\n不要输出解释。" });
        }
        var body = new JsonObject
        {
            ["model"] = "local",
            ["temperature"] = 0,
            ["max_tokens"] = maxTokens,
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = content })
        };
        if (mode == AiStructuredOutputMode.JsonSchemaStrict)
        {
            body["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject { ["name"] = schemaName, ["strict"] = true, ["schema"] = schema }
            };
        }
        return body;
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
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(settings.Settings.AiRequestTimeoutSeconds) };
        var url = $"http://127.0.0.1:{settings.Settings.BusinessAiServerPort}/v1/chat/completions";
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            debugLog.Write("recognition", $"POST {url}; task={taskLabel}; max_tokens={body["max_tokens"]}; attempt={attempt}/2");
            var watch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await http.PostAsync(url, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"), token).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!token.IsCancellationRequested)
            {
                throw new TimeoutException($"AI request timed out after {settings.Settings.AiRequestTimeoutSeconds}s. The current tick was cancelled. Try a smaller model, larger interval, or lower image width.");
            }
            using (response)
            {
            var envelope = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new LlamaCppRequestException($"llama.cpp returned {(int)response.StatusCode}: {envelope}", envelope);
            using var document = JsonDocument.Parse(envelope);
            var choice = document.RootElement.GetProperty("choices")[0];
            var finish = choice.TryGetProperty("finish_reason", out var finishElement) ? finishElement.GetString() : null;
            watch.Stop();
            PublishMetrics(document.RootElement, finish, watch.ElapsedMilliseconds);
            if (finish == "length")
            {
                if (attempt == 1) { body["max_tokens"] = Math.Min((body["max_tokens"]?.GetValue<int>() ?? 512) * 2, 8192); continue; }
                throw new LlamaCppRequestException("llama.cpp exhausted the output token budget twice.", envelope);
            }
            var content = choice.GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content)) throw new LlamaCppRequestException("llama.cpp returned empty content.", envelope);
            return content;
            }
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
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(settings.Settings.AiRequestTimeoutSeconds) }; var url = $"http://127.0.0.1:{settings.Settings.BusinessAiServerPort}/v1/chat/completions";
        logger.LogInformation("Recognition request started. Task={Task}", task);
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            debugLog.Write("recognition", $"POST {url}; task={task}; max_tokens={body["max_tokens"]}; attempt={attempt}/2");
            var watch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await http.PostAsync(url, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"AI request timed out after {settings.Settings.AiRequestTimeoutSeconds}s. The current tick was cancelled. Try a smaller model, larger interval, or lower image width.");
            }
            using (response)
            {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            debugLog.Write("recognition", $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}; response length={raw.Length}");
            if (!response.IsSuccessStatusCode) throw new LlamaCppRequestException($"llama.cpp returned {(int)response.StatusCode}: {raw}", raw);
            try
            {
                using var document = JsonDocument.Parse(raw);
                var choice = document.RootElement.GetProperty("choices")[0];
                var finishReason = choice.TryGetProperty("finish_reason", out var finish) ? finish.GetString() : null;
                watch.Stop();
                PublishMetrics(document.RootElement, finishReason, watch.ElapsedMilliseconds);
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
        }
        throw new InvalidOperationException("Recognition retry loop ended unexpectedly.");
    }

    private void PublishMetrics(JsonElement root, string? finishReason, long elapsedMilliseconds)
    {
        int? prompt = null, completion = null, total = null;
        double? tokensPerSecond = null;
        if (root.TryGetProperty("usage", out var usage))
        {
            prompt = TryGetInt(usage, "prompt_tokens");
            completion = TryGetInt(usage, "completion_tokens");
            total = TryGetInt(usage, "total_tokens");
        }
        if (root.TryGetProperty("timings", out var timings))
            tokensPerSecond = TryGetDouble(timings, "predicted_per_second") ?? TryGetDouble(timings, "tokens_per_second");
        LastFinishReason = finishReason;
        LastResponseMetrics = new(prompt, completion, total, tokensPerSecond, elapsedMilliseconds);
        debugLog.Write("metrics", $"elapsed={elapsedMilliseconds}ms; completion_tokens={completion?.ToString() ?? "not available"}; tokens/s={tokensPerSecond?.ToString("0.##") ?? "not available"}; finish_reason={finishReason ?? "not available"}");
    }

    private static int? TryGetInt(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;

    private static double? TryGetDouble(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.TryGetDouble(out var result) ? result : null;
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
