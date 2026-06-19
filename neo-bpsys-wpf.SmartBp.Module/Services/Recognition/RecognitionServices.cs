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
    public const string SystemPrompt = "You are an IDV/Identity V BP screen recognition engine. Return JSON only. Do not return Markdown. Do not explain. Only use character names from the provided candidate lists. If unsure, return characterName null and confidence below 0.5. Do not invent map data. Do not output MapBP fields.";
    public static string Build(SmartBpRecognitionTask task, IEnumerable<string> survivors, IEnumerable<string> hunters) =>
        $"/no_think\nTask: {task}. Analyze only the supplied BP image. Survivor candidates: {JsonSerializer.Serialize(survivors)}. Hunter candidates: {JsonSerializer.Serialize(hunters)}.";
}

internal static class SmartBpRecognitionJsonSchemaProvider
{
    public static JsonObject Get(SmartBpRecognitionTask task)
    {
        var focused = task is SmartBpRecognitionTask.BanSur or SmartBpRecognitionTask.BanHun or SmartBpRecognitionTask.PickSur or SmartBpRecognitionTask.PickHun;
        var slot = new JsonObject { ["type"] = "object", ["additionalProperties"] = false,
            ["properties"] = new JsonObject { ["slotIndex"] = new JsonObject { ["type"] = "integer" }, ["state"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Picked", "Banned", "Empty", "Unknown") }, ["characterName"] = new JsonObject { ["type"] = new JsonArray("string", "null") }, ["confidence"] = new JsonObject { ["type"] = "number" } },
            ["required"] = new JsonArray("slotIndex", "state", "characterName", "confidence") };
        JsonObject properties = focused
            ? new() { ["schemaVersion"] = Const(1), ["scene"] = Const("Bp"), ["task"] = Const(task.ToString()), ["camp"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Sur", "Hun") }, ["slotIndex"] = new JsonObject { ["type"] = "integer" }, ["characterName"] = new JsonObject { ["type"] = new JsonArray("string", "null") }, ["confidence"] = new JsonObject { ["type"] = "number" }, ["visible"] = new JsonObject { ["type"] = "boolean" }, ["warnings"] = StringArray() }
            : new() { ["schemaVersion"] = Const(1), ["scene"] = Const("Bp"), ["task"] = Const(task.ToString()), ["survivorSlots"] = Array(slot.DeepClone()), ["hunterSlot"] = new JsonObject { ["anyOf"] = new JsonArray(slot.DeepClone(), new JsonObject { ["type"] = "null" }) }, ["survivorBans"] = Array(slot.DeepClone()), ["hunterBans"] = Array(slot.DeepClone()), ["warnings"] = StringArray() };
        var required = focused ? new JsonArray("schemaVersion", "scene", "task", "camp", "slotIndex", "characterName", "confidence", "visible", "warnings") : new JsonArray("schemaVersion", "scene", "task", "warnings");
        return new JsonObject { ["type"] = "object", ["additionalProperties"] = false, ["properties"] = properties, ["required"] = required };
    }
    private static JsonObject Const(object value) => new() { ["const"] = JsonValue.Create(value) };
    private static JsonObject StringArray() => new() { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } };
    private static JsonObject Array(JsonNode? item) => new() { ["type"] = "array", ["items"] = item };
}

internal sealed class LlamaCppOpenAiClient(ISmartBpRecognitionSettingsService settings, ISharedDataService shared, ILogger<LlamaCppOpenAiClient> logger, ISmartBpDebugLog debugLog) : ILlamaCppOpenAiClient
{
    public async Task<string> RecognizeAsync(string imageDataUrl, SmartBpRecognitionTask task, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["model"] = "local", ["temperature"] = 0,
            ["max_tokens"] = task is SmartBpRecognitionTask.BanSur or SmartBpRecognitionTask.BanHun or SmartBpRecognitionTask.PickSur or SmartBpRecognitionTask.PickHun ? settings.Settings.FocusedMaxTokens : settings.Settings.FullScanMaxTokens,
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = SmartBpRecognitionPromptBuilder.SystemPrompt }, new JsonObject { ["role"] = "user", ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = SmartBpRecognitionPromptBuilder.Build(task, shared.SurCharaDict.Keys, shared.HunCharaDict.Keys) }, new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = imageDataUrl } }) }),
            ["response_format"] = new JsonObject { ["type"] = "json_schema", ["json_schema"] = new JsonObject { ["name"] = "smartbp_result", ["strict"] = true, ["schema"] = SmartBpRecognitionJsonSchemaProvider.Get(task) } } };
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) }; var url = $"http://127.0.0.1:{settings.Settings.LlamaServerPort}/v1/chat/completions";
        logger.LogInformation("Recognition request started. Task={Task}", task);
        debugLog.Write("recognition", $"POST {url}; task={task}; max_tokens={body["max_tokens"]}");
        using var response = await http.PostAsync(url, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"), cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        debugLog.Write("recognition", $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}; response length={raw.Length}");
        if (!response.IsSuccessStatusCode) throw new LlamaCppRequestException($"llama.cpp returned {(int)response.StatusCode}: {raw}", raw);
        try
        {
            using var document = JsonDocument.Parse(raw);
            var message = document.RootElement.GetProperty("choices")[0].GetProperty("message");
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
            debugLog.Write("recognition", $"Model JSON content length={content.Length}");
            return content;
        }
        catch (LlamaCppRequestException) { throw; }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new LlamaCppRequestException($"Invalid OpenAI-compatible response envelope: {ex.Message}", raw);
        }
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
            raw = await client.RecognizeAsync(encoder.EncodeDataUrl(frame, settings.Settings.MaxImageWidth), task, cancellationToken);
            var normalized = Parse(raw, task); watch.Stop(); var recommended = Math.Clamp((int)Math.Ceiling(watch.ElapsedMilliseconds * 1.5), settings.Settings.MinRecognitionIntervalMs, settings.Settings.MaxRecognitionIntervalMs);
            logger.LogInformation("Recognition parsed successfully. Task={Task}, ElapsedMs={Elapsed}", task, watch.ElapsedMilliseconds);
            return new(raw, normalized, watch.ElapsedMilliseconds, recommended, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { watch.Stop(); if (ex is LlamaCppRequestException request) raw = request.RawResponse; logger.LogWarning(ex, "Recognition parse/request failed"); return new(raw, "", watch.ElapsedMilliseconds, 0, ex.Message); }
    }

    private string Parse(string raw, SmartBpRecognitionTask expected)
    {
        using var doc = JsonDocument.Parse(raw); var root = doc.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported schemaVersion.");
        if (root.GetProperty("scene").GetString() != "Bp") throw new InvalidDataException("Unknown scene.");
        if (root.GetProperty("task").GetString() != expected.ToString()) throw new InvalidDataException("Unexpected task.");
        var entries = new List<SmartBpNormalizedCharacter>();
        if (expected is SmartBpRecognitionTask.BanSur or SmartBpRecognitionTask.BanHun or SmartBpRecognitionTask.PickSur or SmartBpRecognitionTask.PickHun)
        {
            var campText = root.GetProperty("camp").GetString(); var camp = campText == "Sur" ? Camp.Sur : campText == "Hun" ? Camp.Hun : throw new InvalidDataException("Invalid camp.");
            var expectedCamp = expected is SmartBpRecognitionTask.BanSur or SmartBpRecognitionTask.PickSur ? Camp.Sur : Camp.Hun;
            if (camp != expectedCamp) throw new InvalidDataException("Camp does not match the task.");
            if (root.GetProperty("visible").ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new InvalidDataException("visible must be a JSON boolean.");
            Add(root, camp, entries);
        }
        else
        {
            AddArray(root, "survivorSlots", Camp.Sur, entries); AddArray(root, "survivorBans", Camp.Sur, entries); AddArray(root, "hunterBans", Camp.Hun, entries);
            if (root.TryGetProperty("hunterSlot", out var hunter) && hunter.ValueKind == JsonValueKind.Object) Add(hunter, Camp.Hun, entries);
        }
        return string.Join(Environment.NewLine, entries.Select(x => $"{x.Camp}[{x.SlotIndex}] raw={x.RawCharacterName ?? "null"}; resolved={x.ResolvedCharacterName ?? "unresolved"}; confidence={x.Confidence:0.00}{(x.Warnings.Count > 0 ? "; " + string.Join("; ", x.Warnings) : "")}"));
    }
    private void AddArray(JsonElement root, string name, Camp camp, List<SmartBpNormalizedCharacter> output) { if (!root.TryGetProperty(name, out var array)) return; if (array.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"{name} must be an array."); foreach (var item in array.EnumerateArray()) Add(item, camp, output); }
    private void Add(JsonElement item, Camp camp, List<SmartBpNormalizedCharacter> output) { var slot = item.GetProperty("slotIndex").GetInt32(); if (slot is < 0 or > 15) throw new InvalidDataException("Invalid slot index."); var confidence = item.GetProperty("confidence").GetDouble(); if (confidence is < 0 or > 1) throw new InvalidDataException("Invalid confidence."); string? name = item.TryGetProperty("characterName", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : null; output.Add(resolver.Resolve(name, camp, slot, confidence)); }
}

internal sealed class LlamaCppRequestException(string message, string rawResponse) : Exception(message)
{
    public string RawResponse { get; } = rawResponse;
}
