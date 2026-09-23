using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal static class SmartBpAutomaticParser
{
    /// <summary>
    /// 解析只包含阶段字段的 JSON。
    /// </summary>
    /// <param name="raw">OCR 原始 JSON 文本。</param>
    /// <returns>阶段识别结果。</returns>
    /// <exception cref="InvalidDataException">JSON 结构或阶段值非法时抛出。</exception>
    public static SmartBpPhaseRecognitionResult ParsePhase(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Phase recognition JSON must be an object.");
        var properties = root.EnumerateObject().Select(x => x.Name).ToArray();
        if (!properties.SequenceEqual(["phase"])) throw new InvalidDataException("Phase recognition JSON may only contain the phase field.");
        var phase = root.GetProperty("phase").GetString() ?? "未知";
        if (!SmartBpAutomaticMapping.ValidPhases.Contains(phase)) throw new InvalidDataException("Invalid BP phase.");
        return new() { Phase = phase };
    }

    private static void RejectUnexpectedProperties(JsonObject value, IReadOnlyCollection<string> allowed, string context)
    {
        var unexpected = value.Select(property => property.Key).FirstOrDefault(name => !allowed.Contains(name));
        if (unexpected != null)
            throw new InvalidDataException($"Business OCR fusion output rejected: {context} contained unexpected property {unexpected}.");
    }

    private static void NormalizeLegacyDeltaSlotStates(List<SmartBpSnapshotDeltaSlot>? slots, JsonElement rawUpdate, string propertyName)
    {
        if (slots == null || rawUpdate.ValueKind != JsonValueKind.Object || !rawUpdate.TryGetProperty(propertyName, out var rawSlots) || rawSlots.ValueKind != JsonValueKind.Array) return;
        var rawItems = rawSlots.EnumerateArray().ToArray();
        for (var i = 0; i < slots.Count && i < rawItems.Length; i++)
            NormalizeLegacyDeltaSlotState(slots[i], rawItems[i]);
    }

    private static void NormalizeLegacyDeltaSlotState(SmartBpSnapshotDeltaSlot slot, JsonElement rawUpdate, string propertyName)
    {
        if (rawUpdate.ValueKind == JsonValueKind.Object && rawUpdate.TryGetProperty(propertyName, out var rawSlot))
            NormalizeLegacyDeltaSlotState(slot, rawSlot);
    }

    private static void NormalizeLegacyDeltaSlotState(SmartBpSnapshotDeltaSlot slot, JsonElement rawSlot)
    {
        if (rawSlot.ValueKind == JsonValueKind.Object && !rawSlot.TryGetProperty("slot_state", out _))
            slot.SlotState = SmartBpBusinessStateParser.IsUnselected(slot.CharacterName) ? "empty" : "selected";
    }

    private static void ValidateDeltaSlots(List<SmartBpSnapshotDeltaSlot>? slots, int count, IReadOnlyCollection<string> allowed, string field)
    {
        if (slots == null) throw new InvalidDataException($"{field} update must contain slots.");
        if (slots.Count != count) throw new InvalidDataException($"{field}.slots must contain exactly {count} entries.");
        var expectedIndexes = Enumerable.Range(0, count).ToArray();
        if (!slots.Select(x => x.Index).OrderBy(x => x).SequenceEqual(expectedIndexes))
            throw new InvalidDataException($"{field}.slots contain invalid indexes.");
        foreach (var slot in slots) ValidateDeltaSlot(slot, allowed, field);
    }

    private static void ValidateDeltaSlot(SmartBpSnapshotDeltaSlot slot, IReadOnlyCollection<string> allowed, string field)
    {
        if (string.IsNullOrWhiteSpace(slot.CharacterName) ||
            slot.CharacterName.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
            slot.CharacterName.Equals("null", StringComparison.OrdinalIgnoreCase))
            slot.CharacterName = "未选择";
        else
            slot.CharacterName = slot.CharacterName.Trim();

        slot.SlotState = slot.SlotState?.Trim().ToLowerInvariant() ?? "unknown";
        if (slot.SlotState is not ("selected" or "empty" or "unknown"))
            throw new InvalidDataException($"{field}.slot_state is invalid: {slot.SlotState}");
        if (slot.SlotState == "selected")
        {
            if (SmartBpBusinessStateParser.IsUnselected(slot.CharacterName))
                throw new InvalidDataException($"{field}.selected slot must contain a candidate character.");
            if (!allowed.Contains(slot.CharacterName))
                throw new InvalidDataException($"{field}.character_name is not in the matching candidate list: {slot.CharacterName}");
            return;
        }
        if (slot.SlotState == "empty" && !SmartBpBusinessStateParser.IsUnselected(slot.CharacterName))
            throw new InvalidDataException($"{field}.empty slot must use character_name=未选择.");
        if (slot.SlotState == "unknown" && !SmartBpBusinessStateParser.IsUnselected(slot.CharacterName) && !allowed.Contains(slot.CharacterName))
            throw new InvalidDataException($"{field}.unknown character_name is not in the matching candidate list: {slot.CharacterName}");
    }

    /// <summary>
    /// 将单个字段快照 JSON 响应（形如 <c>{"field":"...","slots":[...]}</c> 或
    /// <c>{"field":"picked_hun","picked_hun":{...}}</c>）解析为 <see cref="SmartBpSnapshotFieldUpdate"/>，
    /// 并校验 slot_state。返回结果只表示本次字段识别，不承担跨帧业务状态存储职责。
    /// </summary>
    /// <param name="raw">模型返回的原始 JSON 响应；使用提示词修复模式时已经过修复。</param>
    /// <param name="expectedField">期望的业务字段标识。</param>
    /// <param name="survivorCandidates">允许的求生者候选名称。</param>
    /// <param name="hunterCandidates">允许的监管者候选名称。</param>
    /// <returns>校验后的 <see cref="SmartBpSnapshotFieldUpdate"/>。</returns>
    /// <exception cref="InvalidDataException">JSON 结构、字段标识、槽位数量、槽位索引或 slot_state 非法时抛出。</exception>
    public static SmartBpSnapshotFieldUpdate ParseFieldSnapshot(
        string raw,
        string expectedField,
        IReadOnlyCollection<string> survivorCandidates,
        IReadOnlyCollection<string> hunterCandidates)
    {
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Field snapshot JSON must be an object.");
        if (!root.TryGetProperty("field", out var fieldElement) || fieldElement.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Field snapshot JSON must contain a string 'field' property.");
        var field = fieldElement.GetString() ?? "";
        if (!string.Equals(field, expectedField, StringComparison.Ordinal))
            throw new InvalidDataException($"Field snapshot field mismatch: expected={expectedField}; actual={field}.");
        var allowed = field switch
        {
            "banned_sur" or "picked_sur" => survivorCandidates,
            "banned_hun" or "picked_hun" => hunterCandidates,
            _ => throw new InvalidDataException($"Invalid field snapshot field: {field}.")
        };
        var update = new SmartBpSnapshotFieldUpdate { Field = field };
        switch (field)
        {
            case "banned_sur":
                {
                    if (!root.TryGetProperty("slots", out var slotsElement) || slotsElement.ValueKind != JsonValueKind.Array)
                        throw new InvalidDataException("banned_sur field snapshot must contain a 'slots' array.");
                    update.Slots = ParseFieldSnapshotSlots(slotsElement, 4, allowed, field);
                    if (root.TryGetProperty("picked_hun", out var pickedHunElement) && pickedHunElement.ValueKind != JsonValueKind.Null)
                        throw new InvalidDataException("banned_sur field snapshot must not contain picked_hun.");
                    break;
                }
            case "banned_hun":
                {
                    if (!root.TryGetProperty("slots", out var slotsElement) || slotsElement.ValueKind != JsonValueKind.Array)
                        throw new InvalidDataException("banned_hun field snapshot must contain a 'slots' array.");
                    update.Slots = ParseFieldSnapshotSlots(slotsElement, 2, allowed, field);
                    if (root.TryGetProperty("picked_hun", out var pickedHunElement) && pickedHunElement.ValueKind != JsonValueKind.Null)
                        throw new InvalidDataException("banned_hun field snapshot must not contain picked_hun.");
                    break;
                }
            case "picked_sur":
                {
                    if (!root.TryGetProperty("slots", out var slotsElement) || slotsElement.ValueKind != JsonValueKind.Array)
                        throw new InvalidDataException("picked_sur field snapshot must contain a 'slots' array.");
                    update.Slots = ParseFieldSnapshotSlots(slotsElement, 4, allowed, field);
                    if (root.TryGetProperty("picked_hun", out var pickedHunElement) && pickedHunElement.ValueKind != JsonValueKind.Null)
                        throw new InvalidDataException("picked_sur field snapshot must not contain picked_hun.");
                    break;
                }
            case "picked_hun":
                {
                    if (root.TryGetProperty("slots", out var slotsElement) && slotsElement.ValueKind != JsonValueKind.Null)
                        throw new InvalidDataException("picked_hun field snapshot must not contain slots.");
                    if (!root.TryGetProperty("picked_hun", out var pickedHunElement) || pickedHunElement.ValueKind != JsonValueKind.Object)
                        throw new InvalidDataException("picked_hun field snapshot must contain a 'picked_hun' object.");
                    var slot = JsonSerializer.Deserialize<SmartBpSnapshotDeltaSlot>(pickedHunElement.GetRawText())
                        ?? throw new InvalidDataException("picked_hun field snapshot object is empty.");
                    if (slot.Index != 0) throw new InvalidDataException("picked_hun.index must be 0.");
                    ValidateDeltaSlot(slot, allowed, "picked_hun");
                    update.PickedHun = slot;
                    break;
                }
        }
        return update;
    }

    private static List<SmartBpSnapshotDeltaSlot> ParseFieldSnapshotSlots(JsonElement slotsElement, int expectedCount, IReadOnlyCollection<string> allowed, string field)
    {
        var slots = JsonSerializer.Deserialize<List<SmartBpSnapshotDeltaSlot>>(slotsElement.GetRawText())
            ?? throw new InvalidDataException($"{field} field snapshot slots array is empty.");
        if (slots.Count != expectedCount)
            throw new InvalidDataException($"{field} field snapshot must contain exactly {expectedCount} slots.");
        var expectedIndexes = Enumerable.Range(0, expectedCount).ToArray();
        if (!slots.Select(x => x.Index).OrderBy(x => x).SequenceEqual(expectedIndexes))
            throw new InvalidDataException($"{field} field snapshot slots contain invalid indexes.");
        foreach (var slot in slots) ValidateDeltaSlot(slot, allowed, field);
        return slots;
    }

    public static SmartBpFocusedBusinessExtractionResult ParseFocusedBusiness(
        string raw,
        GameAction expectedAction,
        IReadOnlyCollection<string> survivorCandidates,
        IReadOnlyCollection<string> hunterCandidates)
    {
        var expected = SmartBpAutomaticMapping.GetFocusedTarget(expectedAction);
        var expectedPhase = SmartBpAutomaticMapping.ToPhase(expectedAction);
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Focused business JSON must be an object.");
        var propertyNames = root.EnumerateObject().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var expectedNames = expected.TargetField == "picked_hun"
            ? new[] { "phase", "picked_hun", "target_field" }
            : ["phase", "slots", "target_field"];
        if (!propertyNames.SequenceEqual(expectedNames.OrderBy(x => x, StringComparer.Ordinal)))
            throw new InvalidDataException("Focused business JSON contains unexpected fields.");

        var phase = root.GetProperty("phase").GetString() ?? "未知";
        var targetField = root.GetProperty("target_field").GetString() ?? "";
        if (phase != expectedPhase) throw new InvalidDataException("Focused business phase does not match the requested crop.");
        if (targetField != expected.TargetField) throw new InvalidDataException("Focused business target_field does not match the requested crop.");

        var result = new SmartBpFocusedBusinessExtractionResult { Phase = phase, TargetField = targetField };
        var allowed = expectedAction is GameAction.BanHun or GameAction.PickHun ? hunterCandidates : survivorCandidates;
        if (targetField == "picked_hun")
        {
            var slot = JsonSerializer.Deserialize<SmartBpRecognizedPlayerCharacterSlot>(root.GetProperty("picked_hun").GetRawText())
                ?? throw new InvalidDataException("picked_hun is empty.");
            if (slot.Index != 0) throw new InvalidDataException("picked_hun.index must be 0.");
            NormalizeFocusedSlot(slot, allowed, "picked_hun");
            result.PickedHun = slot;
            return result;
        }

        var slots = JsonSerializer.Deserialize<List<SmartBpRecognizedPlayerCharacterSlot>>(root.GetProperty("slots").GetRawText())
            ?? throw new InvalidDataException("focused slots are empty.");
        var count = targetField switch { "banned_hun" => 2, "banned_sur" or "picked_sur" => 4, _ => throw new InvalidDataException("Invalid focused target_field.") };
        if (slots.Count != count) throw new InvalidDataException($"{targetField}.slots must contain exactly {count} entries.");
        var expectedIndexes = Enumerable.Range(0, count).ToArray();
        if (!slots.Select(x => x.Index).OrderBy(x => x).SequenceEqual(expectedIndexes))
            throw new InvalidDataException($"{targetField}.slots contain invalid indexes.");
        foreach (var slot in slots) NormalizeFocusedSlot(slot, allowed, targetField);
        result.Slots = slots;
        return result;
    }

    public static SmartBpBusinessStateRecognitionResult ToBusinessState(SmartBpPhaseRecognitionResult phase, SmartBpFocusedBusinessExtractionResult? focused)
    {
        var state = new SmartBpBusinessStateRecognitionResult
        {
            Phase = phase.Phase,
            BannedSur = Enumerable.Range(0, 4).Select(i => new SmartBpRecognizedCharacterSlot { Index = i }).ToList(),
            BannedHun = Enumerable.Range(0, 2).Select(i => new SmartBpRecognizedCharacterSlot { Index = i }).ToList(),
            PickedSur = Enumerable.Range(0, 4).Select(i => new SmartBpRecognizedPlayerCharacterSlot { Index = i }).ToList(),
            PickedHun = new SmartBpRecognizedPlayerCharacterSlot { Index = 0 }
        };
        if (focused == null) return state;
        switch (focused.TargetField)
        {
            case "banned_sur":
                state.BannedSur = focused.Slots.Select(x => new SmartBpRecognizedCharacterSlot { Index = x.Index, CharacterName = x.CharacterName, RecognitionConfidence = x.RecognitionConfidence, IsAutoApplySafe = x.IsAutoApplySafe, RecognitionReason = x.RecognitionReason }).ToList();
                break;
            case "banned_hun":
                state.BannedHun = focused.Slots.Select(x => new SmartBpRecognizedCharacterSlot { Index = x.Index, CharacterName = x.CharacterName, RecognitionConfidence = x.RecognitionConfidence, IsAutoApplySafe = x.IsAutoApplySafe, RecognitionReason = x.RecognitionReason }).ToList();
                break;
            case "picked_sur":
                state.PickedSur = focused.Slots;
                break;
            case "picked_hun":
                state.PickedHun = focused.PickedHun ?? new SmartBpRecognizedPlayerCharacterSlot { Index = 0 };
                break;
        }
        SmartBpBusinessStateParser.NormalizeAndValidate(state);
        return state;
    }

    private static void NormalizeFocusedSlot(SmartBpRecognizedCharacterSlot slot, IReadOnlyCollection<string> allowed, string field)
    {
        if (string.IsNullOrWhiteSpace(slot.CharacterName) ||
            slot.CharacterName.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
            slot.CharacterName.Equals("null", StringComparison.OrdinalIgnoreCase))
            slot.CharacterName = "未选择";
        else
            slot.CharacterName = slot.CharacterName.Trim();

        if (SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)) return;
        if (!allowed.Contains(slot.CharacterName))
            throw new InvalidDataException($"{field}.character_name is not in the matching candidate list: {slot.CharacterName}");
    }

    public static SmartBpStageDetectionResult ParseStage(string raw)
    {
        var result = JsonSerializer.Deserialize<SmartBpStageDetectionResult>(raw)
            ?? throw new InvalidDataException("Stage detection JSON is empty.");
        result.Evidence ??= [];
        result.Warnings ??= [];
        if (result.SchemaVersion != 1) throw new InvalidDataException("Unsupported stage schema_version.");
        if (result.RecognizedAction is not ("BanSur" or "BanHun" or "PickSur" or "DistributeChara" or "PickHun" or "Unknown"))
            throw new InvalidDataException("Invalid recognized_action.");
        if (result.ActiveSide is not ("left" or "right" or "unknown")) throw new InvalidDataException("Invalid active_side.");
        if (result.OperationRegion is not ("left_top" or "left_bottom" or "right_top" or "right_bottom" or "unknown"))
            throw new InvalidDataException("Invalid operation_region.");
        if (result.OperationOwner is not ("survivor" or "hunter" or "unknown")) throw new InvalidDataException("Invalid operation_owner.");
        if (result.TargetCamp is not ("survivor" or "hunter" or "unknown")) throw new InvalidDataException("Invalid target_camp.");
        if (result.Confidence is < 0 or > 1) throw new InvalidDataException("Invalid stage confidence.");
        if (SmartBpAutomaticMapping.TryParseDetectedAction(result.RecognizedAction, out var action))
        {
            var expected = SmartBpAutomaticMapping.Get(action);
            if (result.OperationRegion != expected.Region || result.TargetCamp != expected.Camp)
                throw new InvalidDataException("Detected stage conflicts with the BP region mapping.");
        }
        return result;
    }

    public static SmartBpFocusedExtractionResult ParseFocused(string raw, GameAction expectedAction)
    {
        var result = JsonSerializer.Deserialize<SmartBpFocusedExtractionResult>(raw)
            ?? throw new InvalidDataException("Focused extraction JSON is empty.");
        result.Slots ??= [];
        result.Warnings ??= [];
        var expected = SmartBpAutomaticMapping.Get(expectedAction);
        if (result.SchemaVersion != 1) throw new InvalidDataException("Unsupported focused schema_version.");
        if (result.Task != expectedAction.ToString()) throw new InvalidDataException("Unexpected focused task.");
        if (result.OperationRegion != expected.Region || result.TargetCamp != expected.Camp)
            throw new InvalidDataException("Focused extraction conflicts with the current guidance step.");
        foreach (var slot in result.Slots)
        {
            if (slot.SlotIndex is < -1 or > 15) throw new InvalidDataException("Invalid focused slot index.");
            if (slot.Confidence is < 0 or > 1) throw new InvalidDataException("Invalid focused confidence.");
            if (slot.SlotState is not ("selected" or "waiting" or "unselected" or "banned" or "unknown"))
                throw new InvalidDataException("Invalid focused slot_state.");
        }
        return result;
    }
}
