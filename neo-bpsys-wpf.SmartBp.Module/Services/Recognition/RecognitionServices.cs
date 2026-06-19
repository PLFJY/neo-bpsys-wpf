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
using neo_bpsys_wpf.Core.Abstractions.Services;
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

internal sealed partial class SmartBpCharacterResolver(ISharedDataService shared) : ISmartBpCharacterResolver
{
    public SmartBpNormalizedCharacter Resolve(string? rawName, Camp camp, int slot, double confidence)
    {
        var warnings = new List<string>(); var dict = camp == Camp.Sur ? shared.SurCharaDict : shared.HunCharaDict;
        KeyValuePair<string, Core.Models.Character>? match = null;
        if (!string.IsNullOrWhiteSpace(rawName))
        {
            match = dict.FirstOrDefault(x => x.Key.Equals(rawName, StringComparison.Ordinal));
            if (match.Value.Value == null) match = dict.FirstOrDefault(x => x.Key.Equals(rawName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match.Value.Value == null) { var normalized = Normalize(rawName); match = dict.FirstOrDefault(x => Normalize(x.Key) == normalized); }
        }
        if (match?.Value == null) warnings.Add(string.IsNullOrWhiteSpace(rawName) ? "Character was not visible or recognized." : $"Unresolved character: {rawName}");
        return new(rawName, match?.Value is null ? null : match.Value.Key, match?.Value?.Name, camp, slot, confidence, warnings);
    }
    private static string Normalize(string value) => NonWordRegex().Replace(value, "").ToUpperInvariant();
    [GeneratedRegex(@"[\s\p{P}\p{S}]+", RegexOptions.CultureInvariant)] private static partial Regex NonWordRegex();
}

internal static class SmartBpRecognitionPromptBuilder
{
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
            SmartBpRecognitionTask.BanSur => "当前任务是识别求生者禁用 / 不可选相关区域。优先识别被禁用或不可选的求生者角色，以及可见玩家 ID。",
            SmartBpRecognitionTask.BanHun => "当前任务是识别监管者禁用 / 不可选相关区域。优先识别被禁用或不可选的监管者角色，以及可见玩家 ID。",
            SmartBpRecognitionTask.PickSur => "当前任务是识别求生者选择区域。优先识别已选择、等待中、未选择的求生者槽位，以及玩家 ID。",
            SmartBpRecognitionTask.PickHun => "当前任务是识别监管者选择区域。优先识别监管者槽位、玩家 ID、选择状态。",
            SmartBpRecognitionTask.CharacterDistribution => "当前任务是识别赛前阵容 / 角色分布界面。识别所有可见角色名、玩家 ID、阵营、左右区域与选择状态。",
            _ => "当前任务是完整识别 BP / 阵容选择画面中的所有可见槽位、角色、玩家 ID、区域与状态。"
        };
        return $"""
/no_think
recognition_task: {task}
task_description: {description}
survivor_candidates: {JsonSerializer.Serialize(survivors)}
hunter_candidates: {JsonSerializer.Serialize(hunters)}
必须输出 schema_version、scene、teams、all_characters、all_player_ids、warnings。scene 必须包含 game、interface_type、task、main_status、pause_status、pause_remaining_seconds。每个槽位必须保留 slot_index、slot_state、character_name、player_id、is_banned_or_unavailable、raw_visible_text、confidence。
character_name 与 player_id 必须分开，角色名只能取候选列表原文。不要输出地图 BP，不要输出 MapBP 字段。
""";
    }
}

internal static class SmartBpRecognitionJsonSchemaProvider
{
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
    public static JsonObject Get(SmartBpRecognitionTask task)
    {
        var slot = Object(new JsonObject { ["slot_index"] = Integer(), ["slot_state"] = SlotState(), ["character_name"] = NullableString(), ["player_id"] = NullableString(), ["is_banned_or_unavailable"] = new JsonObject { ["type"] = "boolean" }, ["raw_visible_text"] = NullableString(), ["confidence"] = Confidence() }, "slot_index", "slot_state", "character_name", "player_id", "is_banned_or_unavailable", "raw_visible_text", "confidence");
        var team = Object(new JsonObject { ["side"] = Side(), ["faction"] = Faction(), ["title_text"] = NullableString(), ["subtitle_text"] = NullableString(), ["slots"] = Array(slot) }, "side", "faction", "title_text", "subtitle_text", "slots");
        var character = Object(new JsonObject { ["character_name"] = NullableString(), ["faction"] = Faction(), ["player_id"] = NullableString(), ["side"] = Side(), ["slot_index"] = Integer(), ["slot_state"] = SlotState(), ["confidence"] = Confidence() }, "character_name", "faction", "player_id", "side", "slot_index", "slot_state", "confidence");
        var player = Object(new JsonObject { ["player_id"] = NullableString(), ["character_name"] = NullableString(), ["side"] = Side(), ["slot_index"] = Integer(), ["confidence"] = Confidence() }, "player_id", "character_name", "side", "slot_index", "confidence");
        var scene = Object(new JsonObject { ["game"] = Const("Identity V"), ["interface_type"] = Const("ban_pick_or_lineup_selection"), ["task"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(Enum.GetNames<SmartBpRecognitionTask>().Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) }, ["main_status"] = NullableString(), ["pause_status"] = NullableString(), ["pause_remaining_seconds"] = new JsonObject { ["type"] = new JsonArray("number", "null") } }, "game", "interface_type", "task", "main_status", "pause_status", "pause_remaining_seconds");
        return Object(new JsonObject { ["schema_version"] = Const(1), ["scene"] = scene, ["teams"] = Array(team), ["all_characters"] = Array(character), ["all_player_ids"] = Array(player), ["warnings"] = StringArray() }, "schema_version", "scene", "teams", "all_characters", "all_player_ids", "warnings");
    }
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
            ["response_format"] = new JsonObject { ["type"] = "json_schema", ["json_schema"] = new JsonObject { ["name"] = "smartbp_result", ["strict"] = true, ["schema"] = SmartBpRecognitionJsonSchemaProvider.Get(task) } } };
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
        var result = JsonSerializer.Deserialize<SmartBpVisionExtractionResult>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = false })
            ?? throw new InvalidDataException("Recognition JSON is empty.");
        result.Teams ??= []; result.AllCharacters ??= []; result.AllPlayerIds ??= []; result.Warnings ??= [];
        if (result.SchemaVersion != 1) throw new InvalidDataException("Unsupported schema_version.");
        if (result.Scene.Game != "Identity V" || result.Scene.InterfaceType != "ban_pick_or_lineup_selection") throw new InvalidDataException("Unknown visual extraction scene.");
        if (result.Scene.Task != expected.ToString()) throw new InvalidDataException("Unexpected recognition task.");

        var visual = new StringBuilder();
        var resolved = new StringBuilder();
        visual.AppendLine($"Task: {result.Scene.Task}");
        visual.AppendLine($"Scene status: main={result.Scene.MainStatus ?? "null"} pause={result.Scene.PauseStatus ?? "null"} remaining={result.Scene.PauseRemainingSeconds?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
        visual.AppendLine("Teams:");
        foreach (var team in result.Teams)
        {
            ValidateSide(team.Side); var camp = ParseFaction(team.Faction);
            visual.AppendLine($"- side={team.Side} faction={team.Faction} title={team.TitleText ?? "null"} subtitle={team.SubtitleText ?? "null"}");
            team.Slots ??= [];
            foreach (var slot in team.Slots)
            {
                Validate(slot.SlotIndex, slot.Confidence); ValidateState(slot.SlotState);
                var match = camp == null ? null : resolver.Resolve(slot.CharacterName, camp.Value, slot.SlotIndex, slot.Confidence);
                visual.AppendLine($"  slot[{slot.SlotIndex}] state={slot.SlotState} charRaw={slot.CharacterName ?? "null"} playerId={slot.PlayerId ?? "null"} banned={slot.IsBannedOrUnavailable.ToString().ToLowerInvariant()} conf={slot.Confidence:0.00} rawText={slot.RawVisibleText ?? "null"}");
                resolved.AppendLine($"{team.Faction}[{slot.SlotIndex}] raw={slot.CharacterName ?? "null"}; resolved={match?.ResolvedCharacterName ?? "unresolved"}; playerId={slot.PlayerId ?? "null"}; rawText={slot.RawVisibleText ?? "null"}; confidence={slot.Confidence:0.00}{(match?.Warnings.Count > 0 ? "; " + string.Join("; ", match.Warnings) : "")}");
            }
        }
        visual.AppendLine("All characters:");
        foreach (var character in result.AllCharacters)
        {
            Validate(character.SlotIndex, character.Confidence); ValidateSide(character.Side); ValidateState(character.SlotState);
            var camp = ParseFaction(character.Faction); var match = camp == null ? null : resolver.Resolve(character.CharacterName, camp.Value, character.SlotIndex, character.Confidence);
            visual.AppendLine($"- {character.Faction} slot[{character.SlotIndex}] side={character.Side} raw={character.CharacterName ?? "null"} resolved={match?.ResolvedCharacterName ?? "unresolved"} playerId={character.PlayerId ?? "null"} state={character.SlotState} conf={character.Confidence:0.00}");
        }
        visual.AppendLine("All player IDs:");
        foreach (var player in result.AllPlayerIds)
        {
            Validate(player.SlotIndex, player.Confidence); ValidateSide(player.Side);
            visual.AppendLine($"- slot[{player.SlotIndex}] side={player.Side} playerId={player.PlayerId ?? "null"} character={player.CharacterName ?? "null"} conf={player.Confidence:0.00}");
        }
        visual.AppendLine("Warnings:");
        foreach (var warning in result.Warnings) visual.AppendLine($"- {warning}");
        return (visual.ToString().TrimEnd(), resolved.ToString().TrimEnd());
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
