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

internal static class SmartBpAutomaticMapping
{
    public static readonly IReadOnlyList<string> ValidPhases =
    [
        "屏蔽求生者", "屏蔽监管者", "选择求生者", "求生者选择角色中", "选择监管者",
        "求生者选择天赋中", "监管者选择天赋中", "天赋已锁定",
        "即将进入区域选择", "区域选择", "求生者选择区域中", "监管者选择区域中",
        "等待游戏开始", "加载中", "对局中", "等待中", "未知"
    ];

    public static (string Region, string Camp, string Meaning) Get(GameAction action) => action switch
    {
        GameAction.BanSur => ("right_top", "survivor", "the hunter-side operation area for banning survivors"),
        GameAction.BanHun => ("left_top", "hunter", "the survivor-side operation area for banning hunters"),
        GameAction.PickSur => ("left_bottom", "survivor", "the survivor picking area"),
            GameAction.DistributeChara => ("left_bottom", "survivor", "fixed survivor player slots with assigned characters"),
            GameAction.PickHun => ("right_bottom", "hunter", "the hunter picking area"),
            GameAction.PickSurTalent => ("left_bottom", "survivor", "the survivor talent selection area"),
            GameAction.PickHunTalent => ("right_bottom", "hunter", "the hunter talent selection area"),
            _ => throw new NotSupportedException($"GameGuidance action {action} is not supported by BP recognition.")
        };

    public static SmartBpRecognitionTask ToRecognitionTask(GameAction action) => action switch
    {
        GameAction.BanSur => SmartBpRecognitionTask.BanSur,
        GameAction.BanHun => SmartBpRecognitionTask.BanHun,
        GameAction.PickSur => SmartBpRecognitionTask.PickSur,
            GameAction.DistributeChara => SmartBpRecognitionTask.CharacterDistribution,
            GameAction.PickHun => SmartBpRecognitionTask.PickHun,
        _ => throw new NotSupportedException($"GameGuidance action {action} is not supported by BP recognition.")
    };

    public static bool TryParseDetectedAction(string value, out GameAction action)
    {
        action = value switch
        {
            "BanSur" => GameAction.BanSur,
            "BanHun" => GameAction.BanHun,
            "PickSur" => GameAction.PickSur,
            "DistributeChara" => GameAction.DistributeChara,
            "PickHun" => GameAction.PickHun,
            _ => GameAction.None
        };
        return action != GameAction.None;
    }

    public static bool TryMapPhase(string phase, out GameAction action)
    {
        action = phase switch
        {
            "屏蔽求生者" => GameAction.BanSur,
            "屏蔽监管者" => GameAction.BanHun,
            "选择求生者" => GameAction.PickSur,
            "求生者选择角色中" => GameAction.DistributeChara,
            "选择监管者" => GameAction.PickHun,
            "求生者选择天赋中" => GameAction.PickSurTalent,
            "监管者选择天赋中" => GameAction.PickHunTalent,
            _ => GameAction.None
        };
        return action != GameAction.None;
    }

    public static bool IsCharacterOperationAction(GameAction action) =>
        action is GameAction.BanSur or GameAction.BanHun or GameAction.PickSur or GameAction.DistributeChara or GameAction.PickHun;

    public static string ToPhase(GameAction action) => action switch
    {
        GameAction.BanSur => "屏蔽求生者",
        GameAction.BanHun => "屏蔽监管者",
        GameAction.PickSur => "选择求生者",
        GameAction.DistributeChara => "求生者选择角色中",
        GameAction.PickHun => "选择监管者",
        GameAction.PickSurTalent => "求生者选择天赋中",
        GameAction.PickHunTalent => "监管者选择天赋中",
        _ => "未知"
    };

    public static (SmartBpRecognitionRegion Region, string TargetField) GetFocusedTarget(GameAction action) => action switch
    {
        GameAction.BanSur => (SmartBpRecognitionRegion.RightTop, "banned_sur"),
        GameAction.BanHun => (SmartBpRecognitionRegion.LeftTop, "banned_hun"),
        GameAction.PickSur => (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
        GameAction.DistributeChara => (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
        GameAction.PickHun => (SmartBpRecognitionRegion.RightBottom, "picked_hun"),
        _ => throw new NotSupportedException($"GameGuidance action {action} has no focused character extraction region.")
    };
}

internal static class SmartBpBusinessStateParser
{
    public static SmartBpBusinessStateRecognitionResult Parse(string raw)
    {
        var result = JsonSerializer.Deserialize<SmartBpBusinessStateRecognitionResult>(raw)
            ?? throw new InvalidDataException("Business-state recognition JSON is empty.");
        NormalizeAndValidate(result);
        return result;
    }

    public static void NormalizeAndValidate(SmartBpBusinessStateRecognitionResult result)
    {
        if (!SmartBpAutomaticMapping.ValidPhases.Contains(result.Phase))
            throw new InvalidDataException("Invalid BP phase.");
        result.BannedSur ??= [];
        result.BannedHun ??= [];
        result.PickedSur ??= [];
        result.PickedHun ??= new();
        ValidateCharacterSlots(result.BannedSur, 4, "banned_sur");
        ValidateCharacterSlots(result.BannedHun, 2, "banned_hun");
        ValidatePlayerSlots(result.PickedSur, 4, "picked_sur");
        if (result.PickedHun.Index != 0) throw new InvalidDataException("picked_hun.index must be 0.");
        Normalize(result.PickedHun);
    }

    public static bool IsUnselected(string? value) => string.Equals(NormalizeName(value), "未选择", StringComparison.Ordinal);

    private static void ValidateCharacterSlots(List<SmartBpRecognizedCharacterSlot> slots, int count, string field)
    {
        if (slots.Count != count) throw new InvalidDataException($"{field} must contain exactly {count} entries.");
        var expected = Enumerable.Range(0, count).ToArray();
        if (!slots.Select(x => x.Index).OrderBy(x => x).SequenceEqual(expected))
            throw new InvalidDataException($"{field} must contain indexes {string.Join(",", expected)}.");
        foreach (var slot in slots) Normalize(slot);
    }

    private static void ValidatePlayerSlots(List<SmartBpRecognizedPlayerCharacterSlot> slots, int count, string field)
    {
        if (slots.Count != count) throw new InvalidDataException($"{field} must contain exactly {count} entries.");
        var expected = Enumerable.Range(0, count).ToArray();
        if (!slots.Select(x => x.Index).OrderBy(x => x).SequenceEqual(expected))
            throw new InvalidDataException($"{field} must contain indexes {string.Join(",", expected)}.");
        foreach (var slot in slots) Normalize(slot);
    }

    private static void Normalize(SmartBpRecognizedCharacterSlot slot) => slot.CharacterName = NormalizeName(slot.CharacterName);

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "未选择";
        var trimmed = value.Trim();
        return trimmed.Equals("unknown", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("null", StringComparison.OrdinalIgnoreCase)
            ? "未选择"
            : trimmed;
    }
}

internal static class SmartBpBusinessStateFormatter
{
    public static string Format(SmartBpBusinessStateRecognitionResult value, ISmartBpCharacterResolver resolver, bool includeResolved)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Phase: {value.Phase}");
        AppendCharacterSection(builder, "Banned survivors:", value.BannedSur, Camp.Sur, resolver, includeResolved);
        AppendCharacterSection(builder, "Banned hunters:", value.BannedHun, Camp.Hun, resolver, includeResolved);
        AppendPlayerSection(builder, "Picked survivors:", value.PickedSur, Camp.Sur, resolver, includeResolved);
        AppendPlayerSection(builder, "Picked hunter:", [value.PickedHun], Camp.Hun, resolver, includeResolved, true);
        return builder.ToString().TrimEnd();
    }

    private static void AppendCharacterSection(StringBuilder builder, string title, IEnumerable<SmartBpRecognizedCharacterSlot> slots, Camp camp, ISmartBpCharacterResolver resolver, bool includeResolved)
    {
        builder.AppendLine().AppendLine(title);
        foreach (var slot in slots)
        {
            var resolved = includeResolved && !SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)
                ? resolver.Resolve(slot.CharacterName, camp, slot.Index, 1).ResolvedCharacterName
                : null;
            builder.AppendLine($"[{slot.Index}] {slot.CharacterName}{(resolved == null ? "" : $" / resolved={resolved}")}");
        }
    }

    private static void AppendPlayerSection(StringBuilder builder, string title, IEnumerable<SmartBpRecognizedPlayerCharacterSlot> slots, Camp camp, ISmartBpCharacterResolver resolver, bool includeResolved, bool hunterSlot = false)
    {
        builder.AppendLine().AppendLine(title);
        foreach (var slot in slots)
        {
            var resolved = includeResolved && !SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)
                ? resolver.Resolve(slot.CharacterName, camp, hunterSlot ? -1 : slot.Index, 1).ResolvedCharacterName
                : null;
            builder.AppendLine($"[{slot.Index}] {slot.CharacterName} / {slot.PlayerId ?? "null"}{(resolved == null ? "" : $" / resolved={resolved}")}");
        }
    }
}

internal static class SmartBpAutomaticParser
{
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

    public static SmartBpSnapshotDeltaResult ParseSnapshotDelta(
        string raw,
        IReadOnlyCollection<string> requestedFields,
        IReadOnlyCollection<string> survivorCandidates,
        IReadOnlyCollection<string> hunterCandidates)
    {
        using var document = JsonDocument.Parse(raw);
        var result = JsonSerializer.Deserialize<SmartBpSnapshotDeltaResult>(raw)
            ?? throw new InvalidDataException("Snapshot delta JSON is empty.");
        if (!SmartBpAutomaticMapping.ValidPhases.Contains(result.Phase)) throw new InvalidDataException("Invalid BP phase.");
        result.Updates ??= [];
        var requested = requestedFields.ToHashSet(StringComparer.Ordinal);
        var rawUpdates = document.RootElement.TryGetProperty("updates", out var updatesElement) && updatesElement.ValueKind == JsonValueKind.Array
            ? updatesElement.EnumerateArray().ToArray()
            : [];
        for (var updateIndex = 0; updateIndex < result.Updates.Count; updateIndex++)
        {
            var update = result.Updates[updateIndex];
            var rawUpdate = updateIndex < rawUpdates.Length ? rawUpdates[updateIndex] : default;
            if (!requested.Contains(update.Field)) throw new InvalidDataException($"Snapshot delta contained an unrequested field: {update.Field}.");
            switch (update.Field)
            {
                case "banned_sur":
                    NormalizeLegacyDeltaSlotStates(update.Slots, rawUpdate, "slots");
                    ValidateDeltaSlots(update.Slots, 4, survivorCandidates, update.Field);
                    if (update.PickedHun != null) throw new InvalidDataException("banned_sur update must not contain picked_hun.");
                    break;
                case "banned_hun":
                    NormalizeLegacyDeltaSlotStates(update.Slots, rawUpdate, "slots");
                    ValidateDeltaSlots(update.Slots, 2, hunterCandidates, update.Field);
                    if (update.PickedHun != null) throw new InvalidDataException("banned_hun update must not contain picked_hun.");
                    break;
                case "picked_sur":
                    NormalizeLegacyDeltaSlotStates(update.Slots, rawUpdate, "slots");
                    ValidateDeltaSlots(update.Slots, 4, survivorCandidates, update.Field);
                    if (update.PickedHun != null) throw new InvalidDataException("picked_sur update must not contain picked_hun.");
                    break;
                case "picked_hun":
                    if (update.Slots != null) throw new InvalidDataException("picked_hun update must not contain slots.");
                    if (update.PickedHun == null) throw new InvalidDataException("picked_hun update must contain picked_hun.");
                    if (update.PickedHun.Index != 0) throw new InvalidDataException("picked_hun.index must be 0.");
                    NormalizeLegacyDeltaSlotState(update.PickedHun, rawUpdate, "picked_hun");
                    ValidateDeltaSlot(update.PickedHun, hunterCandidates, "picked_hun");
                    break;
                default:
                    throw new InvalidDataException($"Invalid snapshot delta field: {update.Field}.");
            }
        }
        return result;
    }

    public static SmartBpSnapshotDeltaResult ParseBusinessAiFusionSnapshotDelta(
        string raw,
        string lockedPhase,
        IReadOnlyCollection<string> requestedFields,
        IReadOnlyCollection<string> survivorCandidates,
        IReadOnlyCollection<string> hunterCandidates,
        ICharacterSelectionService characterSelection,
        SmartBpBusinessAiFusionOutputContract outputContract,
        out IReadOnlyList<string> diagnostics)
    {
        var messages = new List<string>();
        var (repaired, removedFence) = SmartBpJsonRepair.Repair(raw);
        if (removedFence)
            messages.Add("Business AI fusion JSON fence was removed before validation.");
        var root = JsonNode.Parse(repaired)?.AsObject()
            ?? throw new InvalidDataException("Business AI fusion output rejected: response must be a JSON object.");
        if (root["phase"] is not JsonValue phaseValue || !phaseValue.TryGetValue<string>(out var outputPhase))
            throw new InvalidDataException("Business AI fusion output rejected: phase must be a string.");
        if (!string.Equals(outputPhase, lockedPhase, StringComparison.Ordinal))
        {
            messages.Add($"Business AI fusion changed phase from {lockedPhase} to {outputPhase}; overridden to {lockedPhase}.");
            root["phase"] = lockedPhase;
        }

        if (HasFullBusinessStateFields(root))
        {
            var delta = NormalizeBusinessAiFusionFullState(root, lockedPhase, requestedFields, survivorCandidates, hunterCandidates, characterSelection, messages);
            messages.Add(outputContract == SmartBpBusinessAiFusionOutputContract.FullBusinessState
                ? "Business AI fusion returned full-state contract; normalized to snapshot delta."
                : "Business AI fusion returned full-state contract while snapshot delta was expected; normalized to snapshot delta.");
            diagnostics = messages;
            return delta;
        }

        RejectUnexpectedProperties(root, ["phase", "updates"], "root");

        if (root["updates"] is JsonObject updatesMap)
        {
            var delta = NormalizeBusinessAiFusionShorthandUpdates(updatesMap, lockedPhase, requestedFields, survivorCandidates, hunterCandidates, characterSelection, messages);
            messages.Add("Business AI fusion returned shorthand updates object; normalized to canonical updates array.");
            diagnostics = messages;
            return delta;
        }

        if (root["updates"] is not JsonArray updates)
            throw new InvalidDataException("Business AI fusion output rejected: updates must be an array.");
        var seenFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in updates)
        {
            if (node is not JsonObject update)
                throw new InvalidDataException("Business AI fusion output rejected: every update must be an object.");
            var field = update["field"]?.GetValue<string>() ?? "";
            RejectUnexpectedProperties(update, ["field", "slots", "picked_hun"], $"update field={field}");
            if (!requestedFields.Contains(field))
                throw new InvalidDataException($"Business AI fusion output rejected: update field={field} was not requested.");
            if (!seenFields.Add(field))
                throw new InvalidDataException($"Business AI fusion output rejected: duplicate update field={field}.");
            if (!update.ContainsKey("slots") || !update.ContainsKey("picked_hun"))
                throw new InvalidDataException($"Business AI fusion output rejected: update field={field} must contain slots and picked_hun.");

            var camp = field is "banned_hun" or "picked_hun" ? Camp.Hun : Camp.Sur;
            var candidates = camp == Camp.Sur ? survivorCandidates : hunterCandidates;
            if (field == "picked_hun")
            {
                if (update["slots"] is not null)
                    throw new InvalidDataException("Business AI fusion output rejected: update field=picked_hun requires slots=null.");
                if (update["picked_hun"] is not JsonObject pickedHunter)
                    throw new InvalidDataException("Business AI fusion output rejected: update field=picked_hun requires a picked_hun object.");
                NormalizeFusionSlot(pickedHunter, field, camp, candidates, characterSelection, messages);
            }
            else
            {
                if (update["picked_hun"] is not null)
                    throw new InvalidDataException($"Business AI fusion output rejected: update field={field} contained unexpected property picked_hun.");
                if (update["slots"] is not JsonArray slots)
                    throw new InvalidDataException($"Business AI fusion output rejected: update field={field} requires a slots array.");
                foreach (var slot in slots.OfType<JsonObject>())
                    NormalizeFusionSlot(slot, field, camp, candidates, characterSelection, messages);
            }
        }
        var missingFields = requestedFields.Where(field => !seenFields.Contains(field)).ToArray();
        if (missingFields.Length > 0)
            throw new InvalidDataException($"Business AI fusion output rejected: missing requested fields [{string.Join(", ", missingFields)}].");

        diagnostics = messages;
        return ParseSnapshotDelta(root.ToJsonString(), requestedFields, survivorCandidates, hunterCandidates);
    }

    private static SmartBpSnapshotDeltaResult NormalizeBusinessAiFusionShorthandUpdates(
        JsonObject updatesMap,
        string lockedPhase,
        IReadOnlyCollection<string> requestedFields,
        IReadOnlyCollection<string> survivorCandidates,
        IReadOnlyCollection<string> hunterCandidates,
        ICharacterSelectionService characterSelection,
        ICollection<string> diagnostics)
    {
        RejectUnexpectedProperties(updatesMap, ["banned_sur", "banned_hun", "picked_sur", "picked_hun"], "updates");
        var updates = NormalizeBusinessAiFusionFieldNodes(
            updatesMap,
            lockedPhase,
            requestedFields,
            survivorCandidates,
            hunterCandidates,
            characterSelection,
            diagnostics);
        return new SmartBpSnapshotDeltaResult { Phase = lockedPhase, Updates = updates };
    }

    private static bool HasFullBusinessStateFields(JsonObject root) =>
        root.ContainsKey("banned_sur") ||
        root.ContainsKey("banned_hun") ||
        root.ContainsKey("picked_sur") ||
        root.ContainsKey("picked_hun");

    private static SmartBpSnapshotDeltaResult NormalizeBusinessAiFusionFullState(
        JsonObject root,
        string lockedPhase,
        IReadOnlyCollection<string> requestedFields,
        IReadOnlyCollection<string> survivorCandidates,
        IReadOnlyCollection<string> hunterCandidates,
        ICharacterSelectionService characterSelection,
        ICollection<string> diagnostics)
    {
        RejectUnexpectedProperties(root, ["phase", "banned_sur", "banned_hun", "picked_sur", "picked_hun"], "root");
        var updates = NormalizeBusinessAiFusionFieldNodes(
            root,
            lockedPhase,
            requestedFields,
            survivorCandidates,
            hunterCandidates,
            characterSelection,
            diagnostics);
        return new SmartBpSnapshotDeltaResult { Phase = lockedPhase, Updates = updates };
    }

    private static List<SmartBpSnapshotFieldUpdate> NormalizeBusinessAiFusionFieldNodes(
        JsonObject fieldNodes,
        string lockedPhase,
        IReadOnlyCollection<string> requestedFields,
        IReadOnlyCollection<string> survivorCandidates,
        IReadOnlyCollection<string> hunterCandidates,
        ICharacterSelectionService characterSelection,
        ICollection<string> diagnostics)
    {
        _ = lockedPhase;
        var requested = requestedFields.ToHashSet(StringComparer.Ordinal);
        var updates = new List<SmartBpSnapshotFieldUpdate>();
        if (fieldNodes.TryGetPropertyValue("banned_sur", out var bannedSur) && requested.Contains("banned_sur"))
            updates.Add(new SmartBpSnapshotFieldUpdate
            {
                Field = "banned_sur",
                Slots = NormalizeFullStateCharacterSlots(bannedSur, 4, Camp.Sur, survivorCandidates, characterSelection, "banned_sur", diagnostics),
                PickedHun = null
            });
        if (fieldNodes.TryGetPropertyValue("banned_hun", out var bannedHun) && requested.Contains("banned_hun"))
            updates.Add(new SmartBpSnapshotFieldUpdate
            {
                Field = "banned_hun",
                Slots = NormalizeFullStateCharacterSlots(bannedHun, 2, Camp.Hun, hunterCandidates, characterSelection, "banned_hun", diagnostics),
                PickedHun = null
            });
        if (fieldNodes.TryGetPropertyValue("picked_sur", out var pickedSur) && requested.Contains("picked_sur"))
            updates.Add(new SmartBpSnapshotFieldUpdate
            {
                Field = "picked_sur",
                Slots = NormalizeFullStatePickedSurSlots(pickedSur, survivorCandidates, characterSelection, diagnostics),
                PickedHun = null
            });
        if (fieldNodes.TryGetPropertyValue("picked_hun", out var pickedHun) && requested.Contains("picked_hun"))
            updates.Add(new SmartBpSnapshotFieldUpdate
            {
                Field = "picked_hun",
                Slots = null,
                PickedHun = NormalizeFullStatePickedHunSlot(pickedHun, hunterCandidates, characterSelection, diagnostics)
            });
        var missingFields = requestedFields.Where(field => updates.All(update => update.Field != field)).ToArray();
        if (missingFields.Length > 0)
            throw new InvalidDataException($"Business AI fusion output rejected: missing requested fields [{string.Join(", ", missingFields)}].");

        var delta = new SmartBpSnapshotDeltaResult { Phase = lockedPhase, Updates = updates };
        foreach (var update in updates)
        {
            switch (update.Field)
            {
                case "banned_sur":
                    ValidateDeltaSlots(update.Slots, 4, survivorCandidates, update.Field);
                    break;
                case "banned_hun":
                    ValidateDeltaSlots(update.Slots, 2, hunterCandidates, update.Field);
                    break;
                case "picked_sur":
                    ValidateDeltaSlots(update.Slots, 4, survivorCandidates, update.Field);
                    break;
                case "picked_hun":
                    if (update.PickedHun == null) throw new InvalidDataException("picked_hun update must contain picked_hun.");
                    if (update.PickedHun.Index != 0) throw new InvalidDataException("picked_hun.index must be 0.");
                    ValidateDeltaSlot(update.PickedHun, hunterCandidates, "picked_hun");
                    break;
            }
        }
        return updates;
    }

    private static List<SmartBpSnapshotDeltaSlot> NormalizeFullStateCharacterSlots(
        JsonNode? node,
        int count,
        Camp camp,
        IReadOnlyCollection<string> candidates,
        ICharacterSelectionService characterSelection,
        string field,
        ICollection<string> diagnostics)
    {
        if (node is not JsonArray array)
            throw new InvalidDataException($"Business AI fusion output rejected: {field} must be an array.");
        var slots = new List<SmartBpSnapshotDeltaSlot>();
        var index = 0;
        foreach (var item in array)
        {
            if (index >= count) break;
            slots.Add(item is JsonObject obj
                ? NormalizeFullStateSlotObject(obj, index, camp, candidates, characterSelection, field, diagnostics)
                : NormalizeFullStateShorthandSlot(GetStringValue(item), index, camp, candidates, characterSelection, field, null, diagnostics));
            index++;
        }
        while (slots.Count < count)
            slots.Add(new SmartBpSnapshotDeltaSlot { Index = slots.Count, SlotState = "unknown", CharacterName = "未选择" });
        return slots;
    }

    private static List<SmartBpSnapshotDeltaSlot> NormalizeFullStatePickedSurSlots(
        JsonNode? node,
        IReadOnlyCollection<string> survivorCandidates,
        ICharacterSelectionService characterSelection,
        ICollection<string> diagnostics)
    {
        if (node is not JsonArray array)
            throw new InvalidDataException("Business AI fusion output rejected: picked_sur must be an array.");
        if (array.All(item => item is not JsonObject) && array.Count >= 8)
        {
            var alternating = array.Select(GetStringValue).ToArray();
            return Enumerable.Range(0, 4)
                .Select(index => NormalizeFullStateShorthandSlot(
                    index * 2 < alternating.Length ? alternating[index * 2] : "",
                    index,
                    Camp.Sur,
                    survivorCandidates,
                    characterSelection,
                    "picked_sur",
                    index * 2 + 1 < alternating.Length ? NormalizePlayerId(alternating[index * 2 + 1]) : null,
                    diagnostics))
                .ToList();
        }
        return NormalizeFullStateCharacterSlots(node, 4, Camp.Sur, survivorCandidates, characterSelection, "picked_sur", diagnostics);
    }

    private static SmartBpSnapshotDeltaSlot NormalizeFullStatePickedHunSlot(
        JsonNode? node,
        IReadOnlyCollection<string> hunterCandidates,
        ICharacterSelectionService characterSelection,
        ICollection<string> diagnostics)
    {
        return node is JsonObject obj
            ? NormalizeFullStateSlotObject(RemoveNullSlotsProperty(obj, "picked_hun"), 0, Camp.Hun, hunterCandidates, characterSelection, "picked_hun", diagnostics)
            : NormalizeFullStateShorthandSlot(GetStringValue(node), 0, Camp.Hun, hunterCandidates, characterSelection, "picked_hun", null, diagnostics);
    }

    private static JsonObject RemoveNullSlotsProperty(JsonObject obj, string field)
    {
        if (obj["slots"] is not null)
            throw new InvalidDataException($"Business AI fusion output rejected: {field}.slots must be null when present.");
        if (!obj.ContainsKey("slots"))
            return obj;
        var clone = new JsonObject();
        foreach (var property in obj)
        {
            if (property.Key == "slots") continue;
            clone[property.Key] = property.Value?.DeepClone();
        }
        return clone;
    }

    private static SmartBpSnapshotDeltaSlot NormalizeFullStateSlotObject(
        JsonObject slot,
        int expectedIndex,
        Camp camp,
        IReadOnlyCollection<string> candidates,
        ICharacterSelectionService characterSelection,
        string field,
        ICollection<string> diagnostics)
    {
        RejectUnexpectedProperties(slot, ["index", "slot_state", "character_name", "player_id"], $"{field} slot");
        var index = GetIntValue(slot["index"], expectedIndex);
        if (index != expectedIndex)
            throw new InvalidDataException($"Business AI fusion output rejected: {field}.index expected {expectedIndex} but was {index}.");
        var rawName = GetStringValue(slot["character_name"]);
        var playerId = NormalizePlayerId(GetStringValue(slot["player_id"]));
        var slotState = GetStringValue(slot["slot_state"]).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slotState))
            return NormalizeFullStateShorthandSlot(rawName, expectedIndex, camp, candidates, characterSelection, field, playerId, diagnostics);
        if (slotState is "empty" or "unknown")
            return new SmartBpSnapshotDeltaSlot { Index = expectedIndex, SlotState = slotState, CharacterName = "未选择", PlayerId = playerId };
        if (slotState != "selected")
            throw new InvalidDataException($"Business AI fusion output rejected: {field}.slot_state is invalid: {slotState}.");
        var normalized = ResolveSelectedCharacter(rawName, camp, candidates, characterSelection, field, diagnostics, allowUnknown: false);
        return new SmartBpSnapshotDeltaSlot { Index = expectedIndex, SlotState = "selected", CharacterName = normalized!, PlayerId = playerId };
    }

    private static SmartBpSnapshotDeltaSlot NormalizeFullStateShorthandSlot(
        string? rawName,
        int index,
        Camp camp,
        IReadOnlyCollection<string> candidates,
        ICharacterSelectionService characterSelection,
        string field,
        string? playerId,
        ICollection<string> diagnostics)
    {
        var name = NormalizeRawText(rawName);
        if (SmartBpBusinessStateParser.IsUnselected(name))
            return new SmartBpSnapshotDeltaSlot { Index = index, SlotState = "empty", CharacterName = "未选择", PlayerId = playerId };
        var normalized = ResolveSelectedCharacter(name, camp, candidates, characterSelection, field, diagnostics, allowUnknown: true);
        return normalized == null
            ? new SmartBpSnapshotDeltaSlot { Index = index, SlotState = "unknown", CharacterName = "未选择", PlayerId = playerId }
            : new SmartBpSnapshotDeltaSlot { Index = index, SlotState = "selected", CharacterName = normalized, PlayerId = playerId };
    }

    private static string? ResolveSelectedCharacter(
        string rawName,
        Camp camp,
        IReadOnlyCollection<string> candidates,
        ICharacterSelectionService characterSelection,
        string field,
        ICollection<string> diagnostics,
        bool allowUnknown)
    {
        if (candidates.Contains(rawName))
            return rawName;
        var resolution = characterSelection.ResolveCharacterDetailed(rawName, camp);
        if (resolution.CanonicalName != null && candidates.Contains(resolution.CanonicalName))
        {
            diagnostics.Add($"Business AI fusion normalized {field} character '{rawName}' to '{resolution.CanonicalName}'.");
            return resolution.CanonicalName;
        }
        if (allowUnknown)
            return null;
        throw new InvalidDataException($"Business AI fusion output rejected: {field}.character_name is not a valid {camp} candidate: {rawName}.");
    }

    private static string NormalizeRawText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "未选择";
        var trimmed = value.Trim();
        return trimmed.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? "未选择"
            : trimmed;
    }

    private static string? NormalizePlayerId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        var normalized = SmartBpOcrTextResolver.NormalizeForMatch(trimmed);
        string[] statusValues =
        [
            "已选择", "未选择", "等待选择", "等待中", "选择中", "天赋已锁定",
            "区域选择", "等待游戏开始", "前往", "剩余"
        ];
        return trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
               statusValues.Any(status => normalized.Equals(
                   SmartBpOcrTextResolver.NormalizeForMatch(status), StringComparison.Ordinal))
            ? null
            : trimmed;
    }

    private static string GetStringValue(JsonNode? node)
    {
        if (node == null) return "";
        return node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : node.ToJsonString().Trim('"');
    }

    private static int GetIntValue(JsonNode? node, int fallback)
    {
        if (node == null) return fallback;
        if (node is JsonValue value && value.TryGetValue<int>(out var intValue))
            return intValue;
        return int.TryParse(GetStringValue(node), out var parsed) ? parsed : fallback;
    }

    private static void RejectUnexpectedProperties(JsonObject value, IReadOnlyCollection<string> allowed, string context)
    {
        var unexpected = value.Select(property => property.Key).FirstOrDefault(name => !allowed.Contains(name));
        if (unexpected != null)
            throw new InvalidDataException($"Business AI fusion output rejected: {context} contained unexpected property {unexpected}.");
    }

    private static void NormalizeFusionSlot(
        JsonObject slot,
        string field,
        Camp camp,
        IReadOnlyCollection<string> candidates,
        ICharacterSelectionService characterSelection,
        ICollection<string> diagnostics)
    {
        RejectUnexpectedProperties(slot, ["index", "slot_state", "character_name", "player_id"], $"{field} slot");
        var slotState = slot["slot_state"]?.GetValue<string>()?.Trim().ToLowerInvariant() ?? "";
        var characterName = slot["character_name"]?.GetValue<string>()?.Trim() ?? "";
        if (slotState is "empty" or "unknown")
        {
            slot["character_name"] = "未选择";
            return;
        }
        if (slotState != "selected" || candidates.Contains(characterName))
            return;
        var resolution = characterSelection.ResolveCharacterDetailed(characterName, camp);
        if (resolution.CanonicalName == null || !candidates.Contains(resolution.CanonicalName))
            throw new InvalidDataException($"Business AI fusion output rejected: {field}.character_name is not a valid {camp} candidate: {characterName}.");
        slot["character_name"] = resolution.CanonicalName;
        diagnostics.Add($"Business AI fusion normalized {field} character '{characterName}' to '{resolution.CanonicalName}'.");
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
    /// Parses one field snapshot JSON response (shape <c>{"field":"...","slots":[...]}</c> or
    /// <c>{"field":"picked_hun","picked_hun":{...}}</c>) into a <see cref="SmartBpSnapshotFieldUpdate"/>
    /// with slot_state validation. The returned update is suitable for <see cref="ISmartBpRecognitionStateStore.ApplyFieldSnapshot"/>.
    /// </summary>
    /// <param name="raw">Raw model JSON response (already repaired when using prompt-and-repair mode).</param>
    /// <param name="expectedField">Expected business field id.</param>
    /// <param name="survivorCandidates">Allowed survivor candidate names.</param>
    /// <param name="hunterCandidates">Allowed hunter candidate names.</param>
    /// <returns>The validated <see cref="SmartBpSnapshotFieldUpdate"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown when the JSON shape, field id, slot count, slot index, or slot_state is invalid.</exception>
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

internal sealed class SmartBpGuidanceSyncService(
    IGameGuidanceService guidance,
    ISmartBpRecognitionSettingsService settings) : ISmartBpGuidanceSyncService
{
    public async Task<SmartBpGuidanceSyncResult> SyncAsync(SmartBpBusinessStateRecognitionResult businessState, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var isTalentLocked = businessState.Phase == "天赋已锁定";
        var action = GameAction.None;
        if (!isTalentLocked && !SmartBpAutomaticMapping.TryMapPhase(businessState.Phase, out action))
            return Reject($"The detected BP phase '{businessState.Phase}' cannot be synchronized.");

        var snapshot = guidance.GetRuntimeSnapshot();
        if (!snapshot.IsStarted)
        {
            var error = await guidance.StartGuidance(settings.Settings.EnableAutoGuidancePageNavigation);
            if (!string.IsNullOrWhiteSpace(error)) return Reject(error);
            snapshot = guidance.GetRuntimeSnapshot();
        }
        if (!snapshot.IsStarted || snapshot.Workflow.Count == 0) return Reject("GameGuidance is not available.");

        if (isTalentLocked)
            return await SyncTalentLockedAsync(snapshot, cancellationToken);

        GameGuidanceStepSnapshot? target = null;
        if (snapshot.CurrentAction == action && snapshot.CurrentStepIndex >= 0)
            target = snapshot.Workflow.FirstOrDefault(x => x.StepIndex == snapshot.CurrentStepIndex);
        if (target == null)
        {
            var last = Math.Min(snapshot.Workflow.Count - 1,
                Math.Max(snapshot.CurrentStepIndex, -1) + settings.Settings.GuidanceSyncLookAheadSteps);
            target = snapshot.Workflow
                .Where(x => x.StepIndex > snapshot.CurrentStepIndex && x.StepIndex <= last && x.Action == action)
                .OrderBy(x => x.StepIndex)
                .FirstOrDefault();
        }
        if (target == null) return Reject($"No forward {action} step exists within the configured lookahead window.", action);
        if (target.StepIndex == snapshot.CurrentStepIndex)
            return new(false, true, "Current GameGuidance step already matches the detected stage.", target.Action, target.Indexes, target.StepIndex);

        cancellationToken.ThrowIfCancellationRequested();
        var moveError = await guidance.MoveToStepAsync(target.StepIndex, settings.Settings.EnableAutoGuidancePageNavigation);
        if (!string.IsNullOrWhiteSpace(moveError)) return Reject(moveError, action);
        return new(true, true, $"GameGuidance moved forward to step {target.StepIndex}.", target.Action, target.Indexes, target.StepIndex);
    }

    private async Task<SmartBpGuidanceSyncResult> SyncTalentLockedAsync(GameGuidanceRuntimeSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.CurrentAction is GameAction.PickSurTalent or GameAction.PickHunTalent)
            return new(false, true, "Current GameGuidance step already matches the locked talent phase.", snapshot.CurrentAction, snapshot.CurrentIndexes, snapshot.CurrentStepIndex);

        var last = Math.Min(snapshot.Workflow.Count - 1,
            Math.Max(snapshot.CurrentStepIndex, -1) + settings.Settings.GuidanceSyncLookAheadSteps);
        var candidates = snapshot.Workflow
            .Where(x => x.StepIndex > snapshot.CurrentStepIndex && x.StepIndex <= last &&
                        x.Action is GameAction.PickSurTalent or GameAction.PickHunTalent or GameAction.EndGuidance)
            .OrderBy(x => x.StepIndex)
            .ToArray();
        if (candidates.Length == 0) return Reject("No forward talent or end step exists within the configured lookahead window.");
        if (candidates.Length > 1) return Reject("Talent locked phase is ambiguous; not syncing automatically.");

        var target = candidates[0];
        cancellationToken.ThrowIfCancellationRequested();
        var moveError = await guidance.MoveToStepAsync(target.StepIndex, settings.Settings.EnableAutoGuidancePageNavigation);
        if (!string.IsNullOrWhiteSpace(moveError)) return Reject(moveError, target.Action);
        return new(true, true, $"GameGuidance moved forward to locked talent context step {target.StepIndex}.", target.Action, target.Indexes, target.StepIndex);
    }

    private static SmartBpGuidanceSyncResult Reject(string reason, GameAction? action = null) =>
        new(false, false, reason, action, [], null);
}

internal sealed class SmartBpCandidateOperationBuilder(ISmartBpCharacterResolver resolver, ISharedDataService shared)
{
    public IReadOnlyList<SmartBpDetectedOperation> Build(
        SmartBpBusinessStateRecognitionResult state,
        GameAction action,
        IReadOnlyList<int> guidanceIndexes)
        => BuildWithDiagnostics(state, action, guidanceIndexes).Operations;

    public SmartBpCandidateOperationBuildResult BuildWithDiagnostics(
        SmartBpBusinessStateRecognitionResult state,
        GameAction action,
        IReadOnlyList<int> guidanceIndexes)
    {
        if (!SmartBpAutomaticMapping.IsCharacterOperationAction(action))
            return new([], [$"Detected phase is a talent/lock phase ({state.Phase}); no character operation is generated."]);
        return action switch
        {
            GameAction.BanSur => BuildFromCharacterSlots(state.BannedSur, action, guidanceIndexes, Camp.Sur, SmartBpDetectedOperationKind.BanCharacter),
            GameAction.BanHun => BuildFromCharacterSlots(state.BannedHun, action, guidanceIndexes, Camp.Hun, SmartBpDetectedOperationKind.BanCharacter),
            GameAction.PickSur => BuildFromPlayerSlots(state.PickedSur, action, guidanceIndexes, Camp.Sur, SmartBpDetectedOperationKind.PickSurvivor),
            GameAction.DistributeChara => BuildDistribution(state.PickedSur, guidanceIndexes),
            GameAction.PickHun => BuildFromPlayerSlots([state.PickedHun], action, guidanceIndexes, Camp.Hun, SmartBpDetectedOperationKind.PickHunter, true),
            _ => new([], [$"Current GameGuidance action {action} is not a BP character operation."])
        };
    }

    public SmartBpCandidateOperationBuildResult BuildWithDiagnostics(
        SmartBpFocusedBusinessExtractionResult focused,
        GameAction action,
        IReadOnlyList<int> guidanceIndexes)
    {
        if (!SmartBpAutomaticMapping.IsCharacterOperationAction(action))
            return new([], [$"Detected phase is a talent/lock phase ({focused.Phase}); no character operation is generated."]);
        return action switch
        {
            GameAction.BanSur => BuildFromCharacterSlots(focused.Slots, action, guidanceIndexes, Camp.Sur, SmartBpDetectedOperationKind.BanCharacter),
            GameAction.BanHun => BuildFromCharacterSlots(focused.Slots, action, guidanceIndexes, Camp.Hun, SmartBpDetectedOperationKind.BanCharacter),
            GameAction.PickSur => BuildFromPlayerSlots(focused.Slots, action, guidanceIndexes, Camp.Sur, SmartBpDetectedOperationKind.PickSurvivor),
            GameAction.DistributeChara => BuildDistribution(focused.Slots, guidanceIndexes),
            GameAction.PickHun => focused.PickedHun == null
                ? new([], ["Focused picked_hun result did not contain picked_hun."])
                : BuildFromPlayerSlots([focused.PickedHun], action, guidanceIndexes, Camp.Hun, SmartBpDetectedOperationKind.PickHunter, true),
            _ => new([], [$"Current GameGuidance action {action} is not a BP character operation."])
        };
    }

    private SmartBpCandidateOperationBuildResult BuildFromCharacterSlots(IEnumerable<SmartBpRecognizedCharacterSlot> slots, GameAction action, IReadOnlyList<int> guidanceIndexes, Camp camp, SmartBpDetectedOperationKind kind)
    {
        var operations = new List<SmartBpDetectedOperation>();
        var messages = new List<string>();
        foreach (var slot in slots)
        {
            if (SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)) continue;
            if (guidanceIndexes.Count > 0 && !guidanceIndexes.Contains(slot.Index))
            {
                messages.Add($"Skipped: index not in current GameGuidance indexes ({camp}[{slot.Index}] {slot.CharacterName}).");
                continue;
            }
            var confidence = slot.IsAutoApplySafe ? slot.RecognitionConfidence : Math.Min(slot.RecognitionConfidence, .89);
            var resolved = resolver.Resolve(slot.CharacterName, camp, slot.Index, confidence);
            operations.Add(new(kind, action, guidanceIndexes.ToArray(), camp, slot.Index, slot.CharacterName,
                resolved.ResolvedCharacterKey, resolved.ResolvedCharacterName, null, confidence,
                slot.RecognitionReason ?? $"Business-state snapshot phase {action} produced slot {slot.Index}."));
        }
        return new(operations, messages);
    }

    private SmartBpCandidateOperationBuildResult BuildFromPlayerSlots(IEnumerable<SmartBpRecognizedPlayerCharacterSlot> slots, GameAction action, IReadOnlyList<int> guidanceIndexes, Camp camp, SmartBpDetectedOperationKind kind, bool hunterSlot = false)
    {
        var operations = new List<SmartBpDetectedOperation>();
        var messages = new List<string>();
        foreach (var slot in slots)
        {
            if (SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)) continue;
            var internalSlot = hunterSlot ? -1 : slot.Index;
            if (!hunterSlot && guidanceIndexes.Count > 0 && !guidanceIndexes.Contains(internalSlot))
            {
                messages.Add($"Skipped: index not in current GameGuidance indexes ({camp}[{internalSlot}] {slot.CharacterName}).");
                continue;
            }
            var confidence = slot.IsAutoApplySafe ? slot.RecognitionConfidence : Math.Min(slot.RecognitionConfidence, .89);
            var resolved = resolver.Resolve(slot.CharacterName, camp, internalSlot, confidence);
            operations.Add(new(kind, action, guidanceIndexes.ToArray(), camp, internalSlot, slot.CharacterName,
                resolved.ResolvedCharacterKey, resolved.ResolvedCharacterName, slot.PlayerId, confidence,
                slot.RecognitionReason ?? (hunterSlot ? "Business-state snapshot mapped hunter visual slot 0 to internal hunter slot -1." : $"Business-state snapshot phase {action} produced slot {internalSlot}.")));
        }
        return new(operations, messages);
    }

    public IReadOnlyList<SmartBpDetectedOperation> Build(
        SmartBpFocusedExtractionResult extraction,
        GameAction action,
        IReadOnlyList<int> guidanceIndexes)
    {
        if (action == GameAction.DistributeChara)
            return BuildDistribution(extraction, guidanceIndexes);
        var operations = new List<SmartBpDetectedOperation>();
        var camp = action is GameAction.BanHun or GameAction.PickHun ? Camp.Hun : Camp.Sur;
        foreach (var slot in extraction.Slots)
        {
            if (slot.CharacterName == null || SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)) continue;
            var internalSlot = action == GameAction.PickHun ? -1 : slot.SlotIndex;
            if (action is GameAction.BanSur or GameAction.BanHun or GameAction.PickSur &&
                guidanceIndexes.Count > 0 && !guidanceIndexes.Contains(internalSlot))
                continue;
            if (action == GameAction.DistributeChara && internalSlot is < 0 or > 3) continue;
            var resolved = resolver.Resolve(slot.CharacterName, camp, internalSlot, slot.Confidence);
            var kind = action switch
            {
                GameAction.BanSur or GameAction.BanHun => SmartBpDetectedOperationKind.BanCharacter,
                GameAction.PickSur => SmartBpDetectedOperationKind.PickSurvivor,
                GameAction.PickHun => SmartBpDetectedOperationKind.PickHunter,
                GameAction.DistributeChara => SmartBpDetectedOperationKind.SwapSurvivors,
                _ => throw new NotSupportedException()
            };
            var reason = kind == SmartBpDetectedOperationKind.SwapSurvivors
                ? $"Place the detected character into fixed survivor player slot {internalSlot}."
                : $"Focused {action} extraction matched the authoritative guidance step.";
            operations.Add(new(kind, action, guidanceIndexes.ToArray(), camp, internalSlot,
                slot.CharacterName, resolved.ResolvedCharacterKey, resolved.ResolvedCharacterName,
                slot.PlayerId, slot.Confidence, reason));
        }
        return operations;
    }

    private SmartBpCandidateOperationBuildResult BuildDistribution(
        IEnumerable<SmartBpRecognizedPlayerCharacterSlot> slots,
        IReadOnlyList<int> guidanceIndexes)
    {
        var operations = new List<SmartBpDetectedOperation>();
        var messages = new List<string>();
        var simulated = shared.CurrentGame.SurPlayerList.Select(x => x.Character?.Name).ToArray();
        foreach (var slot in slots.Where(x => !SmartBpBusinessStateParser.IsUnselected(x.CharacterName) && x.Index is >= 0 and < 4).OrderBy(x => x.Index))
        {
            var confidence = slot.IsAutoApplySafe ? slot.RecognitionConfidence : Math.Min(slot.RecognitionConfidence, .89);
            var resolved = resolver.Resolve(slot.CharacterName, Camp.Sur, slot.Index, confidence);
            if (resolved.ResolvedCharacterName != null && simulated[slot.Index] == resolved.ResolvedCharacterName)
            {
                messages.Add($"Skipped: no-op same character Sur[{slot.Index}] {slot.CharacterName}.");
                continue;
            }
            operations.Add(new(SmartBpDetectedOperationKind.SwapSurvivors, GameAction.DistributeChara,
                guidanceIndexes.ToArray(), Camp.Sur, slot.Index, slot.CharacterName,
                resolved.ResolvedCharacterKey, resolved.ResolvedCharacterName, slot.PlayerId,
                confidence, slot.RecognitionReason ?? $"Place the detected character into fixed survivor player slot {slot.Index}."));
            if (resolved.ResolvedCharacterName == null) continue;
            var source = Array.FindIndex(simulated, x => x == resolved.ResolvedCharacterName);
            if (source < 0) continue;
            (simulated[source], simulated[slot.Index]) = (simulated[slot.Index], simulated[source]);
        }
        return new(operations, messages);
    }

    private IReadOnlyList<SmartBpDetectedOperation> BuildDistribution(
        SmartBpFocusedExtractionResult extraction,
        IReadOnlyList<int> guidanceIndexes)
    {
        var operations = new List<SmartBpDetectedOperation>();
        var simulated = shared.CurrentGame.SurPlayerList.Select(x => x.Character?.Name).ToArray();
        foreach (var slot in extraction.Slots.Where(x => x.CharacterName != null && !SmartBpBusinessStateParser.IsUnselected(x.CharacterName) && x.SlotIndex is >= 0 and < 4).OrderBy(x => x.SlotIndex))
        {
            var resolved = resolver.Resolve(slot.CharacterName, Camp.Sur, slot.SlotIndex, slot.Confidence);
            if (resolved.ResolvedCharacterName != null && simulated[slot.SlotIndex] == resolved.ResolvedCharacterName) continue;
            operations.Add(new(SmartBpDetectedOperationKind.SwapSurvivors, GameAction.DistributeChara,
                guidanceIndexes.ToArray(), Camp.Sur, slot.SlotIndex, slot.CharacterName,
                resolved.ResolvedCharacterKey, resolved.ResolvedCharacterName, slot.PlayerId,
                slot.Confidence, $"Place the detected character into fixed survivor player slot {slot.SlotIndex}."));
            if (resolved.ResolvedCharacterName == null) continue;
            var source = Array.FindIndex(simulated, x => x == resolved.ResolvedCharacterName);
            if (source < 0) continue;
            (simulated[source], simulated[slot.SlotIndex]) = (simulated[slot.SlotIndex], simulated[source]);
        }
        return operations;
    }
}

internal sealed class SmartBpDetectedOperationApplier(
    ICharacterSelectionService selection,
    IGameGuidanceService guidance,
    ISharedDataService shared,
    ISmartBpRecognitionSettingsService settings,
    ISmartBpRecognitionLedger ledger) : ISmartBpDetectedOperationApplier
{
    public async Task<SmartBpOperationApplyResult> ApplyAsync(IReadOnlyList<SmartBpDetectedOperation> operations, CancellationToken cancellationToken = default)
    {
        var messages = new List<string>();
        var applied = 0;
        var skipped = 0;
        if (operations.Count == 0)
            return new(0, 0, ["No candidate operations to apply."]);
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = guidance.GetRuntimeSnapshot();
            var key = SmartBpWorkflowBackfillService.CreateKey(shared.CurrentGame.GameProgress, operation);
            if (key != null && ledger.IsStepOperationCompleted(key)) { skipped++; messages.Add($"Skipped: recognition ledger already completed {Describe(operation)}."); continue; }
            if (!ValidateWorkflowSource(operation, snapshot, out var workflowError)) { skipped++; MarkSkipped(key, workflowError); messages.Add($"Skipped: {workflowError} for {Describe(operation)}."); continue; }
            if (operation.Confidence < 0.90) { skipped++; MarkSkipped(key, "low confidence"); messages.Add($"Skipped: low confidence for {Describe(operation)}."); continue; }
            if (operation.ResolvedCharacterKey == null) { skipped++; MarkSkipped(key, "unresolved character"); messages.Add($"Skipped: unresolved character for {Describe(operation)}."); continue; }
            var dictionary = operation.Camp == Camp.Sur ? shared.SurCharaDict : shared.HunCharaDict;
            if (!dictionary.TryGetValue(operation.ResolvedCharacterKey, out var character)) { skipped++; MarkSkipped(key, "resolved key missing"); messages.Add($"Skipped: resolved character key no longer exists: {operation.ResolvedCharacterKey}."); continue; }
            var playAnimation = operation.ApplyMode == SmartBpDetectedOperationApplyMode.CurrentStep ||
                                operation.ApplyMode == SmartBpDetectedOperationApplyMode.Backfill &&
                                settings.Settings.RecognitionEngine != SmartBpRecognitionEngine.AiQwen &&
                                settings.Settings.PlayBackfillAnimations;
            if (playAnimation && operation.ApplyMode == SmartBpDetectedOperationApplyMode.CurrentStep && settings.Settings.RecognitionVisualBufferMilliseconds > 0)
                await Task.Delay(settings.Settings.RecognitionVisualBufferMilliseconds, cancellationToken);

            switch (operation.Kind)
            {
                case SmartBpDetectedOperationKind.BanCharacter:
                    if (!TryGetBanSlot(operation.Camp, operation.SlotIndex, out var banned))
                    {
                        skipped++;
                        MarkSkipped(key, "invalid ban slot");
                        messages.Add($"Skipped: invalid ban slot for {Describe(operation)}.");
                        continue;
                    }
                    if (IsSameCharacter(banned, character))
                    {
                        skipped++;
                        MarkCompleted(key);
                        messages.Add($"Skipped: no-op same ban for {Describe(operation)}.");
                        continue;
                    }
                    await selection.BanCharacterAsync(operation.Camp, operation.SlotIndex, character, playAnimation);
                    messages.Add($"{AppliedPrefix(operation, playAnimation)} BanCharacter {operation.Camp}[{operation.SlotIndex}] {character.Name}");
                    break;
                case SmartBpDetectedOperationKind.PickSurvivor:
                    if (operation.SlotIndex is < 0 or >= 4)
                    {
                        skipped++;
                        MarkSkipped(key, "invalid survivor slot");
                        messages.Add($"Skipped: invalid survivor slot for {Describe(operation)}.");
                        continue;
                    }
                    if (IsSameCharacter(shared.CurrentGame.SurPlayerList[operation.SlotIndex].Character, character))
                    {
                        skipped++;
                        MarkCompleted(key);
                        messages.Add($"Skipped: no-op same character for {Describe(operation)}.");
                        continue;
                    }
                    await selection.SelectSurvivorAsync(operation.SlotIndex, character, playAnimation);
                    messages.Add($"{AppliedPrefix(operation, playAnimation)} PickSurvivor Sur[{operation.SlotIndex}] {character.Name}");
                    break;
                case SmartBpDetectedOperationKind.PickHunter:
                    if (IsSameCharacter(shared.CurrentGame.HunPlayer.Character, character))
                    {
                        skipped++;
                        MarkCompleted(key);
                        messages.Add($"Skipped: no-op same character for {Describe(operation)}.");
                        continue;
                    }
                    await selection.SelectHunterAsync(character, playAnimation);
                    messages.Add($"{AppliedPrefix(operation, playAnimation)} PickHunter {character.Name}");
                    break;
                case SmartBpDetectedOperationKind.SwapSurvivors:
                    if (operation.SlotIndex is < 0 or >= 4)
                    {
                        skipped++;
                        MarkSkipped(key, "invalid survivor swap target");
                        messages.Add($"Skipped: invalid survivor swap target for {Describe(operation)}.");
                        continue;
                    }
                    if (IsSameCharacter(shared.CurrentGame.SurPlayerList[operation.SlotIndex].Character, character))
                    {
                        skipped++;
                        MarkCompleted(key);
                        messages.Add($"Skipped: no-op same character for {Describe(operation)}.");
                        continue;
                    }
                    var sourceMatch = shared.CurrentGame.SurPlayerList
                        .Select((player, index) => (player, index))
                        .FirstOrDefault(x => IsSameCharacter(x.player.Character, character));
                    if (sourceMatch.player == null) { skipped++; MarkSkipped(key, "no source slot contains target character"); messages.Add($"Skipped: no source slot contains target character for {Describe(operation)}."); continue; }
                    var source = sourceMatch.index;
                    if (source == operation.SlotIndex)
                    {
                        skipped++;
                        MarkCompleted(key);
                        messages.Add($"Skipped: no-op swap source and target are the same for {Describe(operation)}.");
                        continue;
                    }
                    await selection.SwapSurvivorsAsync(source, operation.SlotIndex, playAnimation);
                    messages.Add($"{AppliedPrefix(operation, playAnimation)} SwapSurvivors source={source} target={operation.SlotIndex} {character.Name}");
                    break;
            }
            applied++;
            MarkCompleted(key);
        }
        return new(applied, skipped, messages);
    }

    private static bool ValidateWorkflowSource(
        SmartBpDetectedOperation operation,
        GameGuidanceRuntimeSnapshot snapshot,
        out string error)
    {
        if (operation.ApplyMode == SmartBpDetectedOperationApplyMode.FreeSync)
        {
            var valid = operation.Kind switch
            {
                SmartBpDetectedOperationKind.BanCharacter => operation.Camp is Camp.Sur or Camp.Hun && operation.SlotIndex >= 0,
                SmartBpDetectedOperationKind.PickSurvivor => operation.Camp == Camp.Sur && operation.SlotIndex is >= 0 and < 4,
                SmartBpDetectedOperationKind.PickHunter => operation.Camp == Camp.Hun && operation.SlotIndex == -1,
                _ => false
            };
            error = valid ? "" : "invalid free-sync operation contract";
            return valid;
        }
        if (operation.ApplyMode == SmartBpDetectedOperationApplyMode.CurrentStep)
        {
            if (snapshot.CurrentAction != operation.SourceGuidanceAction) { error = "current GameGuidance action mismatch"; return false; }
            if (!snapshot.CurrentIndexes.SequenceEqual(operation.SourceGuidanceIndexes)) { error = "GameGuidance indexes changed"; return false; }
            error = "";
            return true;
        }

        if (operation.SourceWorkflowStepIndex is not { } stepIndex) { error = "backfill source workflow step is missing"; return false; }
        var step = snapshot.Workflow.FirstOrDefault(item => item.StepIndex == stepIndex);
        if (step == null) { error = "backfill source workflow step no longer exists"; return false; }
        if (step.Action != operation.SourceGuidanceAction || !step.Indexes.SequenceEqual(operation.SourceGuidanceIndexes))
        {
            error = "backfill source workflow action or indexes changed";
            return false;
        }
        error = "";
        return true;
    }

    private void MarkCompleted(SmartBpWorkflowOperationKey? key)
    {
        if (key != null) ledger.MarkCompleted(key);
    }

    private void MarkSkipped(SmartBpWorkflowOperationKey? key, string reason)
    {
        if (key != null) ledger.MarkSkipped(key, reason);
    }

    private static string AppliedPrefix(SmartBpDetectedOperation operation, bool playAnimation) =>
        operation.ApplyMode == SmartBpDetectedOperationApplyMode.Backfill
            ? $"Backfilled{(playAnimation ? " with animation" : " without animation")}: Applied"
            : "Applied";

    private bool TryGetBanSlot(Camp camp, int slotIndex, out Character? character)
    {
        var list = camp == Camp.Sur ? shared.CurrentGame.CurrentSurBannedList : shared.CurrentGame.CurrentHunBannedList;
        if (slotIndex < 0 || slotIndex >= list.Count)
        {
            character = null;
            return false;
        }

        character = list[slotIndex];
        return true;
    }

    private static bool IsSameCharacter(Character? left, Character? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null) return false;
        if (!string.IsNullOrWhiteSpace(left.ImageFileName) && !string.IsNullOrWhiteSpace(right.ImageFileName))
            return string.Equals(left.ImageFileName, right.ImageFileName, StringComparison.Ordinal);
        return !string.IsNullOrWhiteSpace(left.Name) &&
               !string.IsNullOrWhiteSpace(right.Name) &&
               string.Equals(left.Name, right.Name, StringComparison.Ordinal);
    }

    private static string Describe(SmartBpDetectedOperation operation) =>
        $"{operation.Kind} {operation.Camp}[{operation.SlotIndex}] {operation.RawCharacterName ?? "null"}";
}

internal enum SmartBpRecognitionDebugMode
{
    Automatic,
    FullStrategy,
    CurrentStageIncremental
}

internal sealed class SmartBpAutoRecognitionCoordinator(
    ISmartBpRegionSnapshotRecognitionService snapshotRecognition,
    ISmartBpSnapshotDeltaRecognitionService deltaRecognition,
    ISmartBpAiFieldSnapshotRecognitionService fieldSnapshotRecognition,
    ISmartBpSnapshotRecognitionPlanner planner,
    ISmartBpRecognitionStateStore stateStore,
    ISmartBpRecognitionLedger ledger,
    ISmartBpFrameRingBuffer frameRingBuffer,
    ISmartBpRecognitionSettingsService settings,
    ISharedDataService shared,
    ISmartBpGuidanceSyncService guidanceSync,
    IGameGuidanceService guidance,
    ISmartBpWorkflowBackfillService backfill,
    SmartBpCandidateOperationBuilder candidateBuilder,
    ISmartBpDetectedOperationApplier applier,
    ISmartBpSceneGateService sceneGate,
    ISmartBpOcrBpRecognitionService ocrRecognition,
    ISmartBpAiOcrTranscriptRecognitionService aiOcrTranscriptRecognition,
    ISmartBpAiOcrTranscriptInterpreter aiOcrTranscriptInterpreter,
    ISmartBpBusinessAiFusionService businessAiFusion,
    ILlamaCppServerManagerFactory llamaServerManagers,
    ISmartBpDebugLog debugLog) : ISmartBpAutoRecognitionCoordinator, ISmartBpStepCommitScheduler
{
    private readonly SemaphoreSlim _tickGate = new(1, 1);
    private readonly object _cancellationLock = new();
    private CancellationTokenSource? _runCancellation;
    private CancellationTokenSource? _currentTickCancellation;
    private string? _lastSnapshotFingerprint;
    private int _stableSnapshotCount;
    private long _frameSequence;
    private GameAction? _lastExplicitAction;
    private int _unknownPhaseFrames;
    private bool _hasDetectedPostBp;
    private string _postBpPhase = "未知";
    private long _postBpDetectedFrameSequence;
    private int _transitionToAreaSelectionConsecutiveCount;
    private SmartBpLifecycleCategory _lastStableLifecycleCategory = SmartBpLifecycleCategory.Unknown;
    public bool IsRunning => _runCancellation is { IsCancellationRequested: false };

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_cancellationLock)
        {
            _runCancellation?.Cancel();
            _runCancellation?.Dispose();
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }
        _lastSnapshotFingerprint = null;
        _stableSnapshotCount = 0;
        _lastExplicitAction = null;
        _unknownPhaseFrames = 0;
        ClearPostBpLatch();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (_cancellationLock)
        {
            _runCancellation?.Cancel();
            _currentTickCancellation?.Cancel();
        }
        ClearPostBpLatch();
        return Task.CompletedTask;
    }

    public Task CompleteAsync()
    {
        lock (_cancellationLock)
        {
            _runCancellation?.Dispose();
            _runCancellation = null;
        }
        ClearPostBpLatch();
        return Task.CompletedTask;
    }

    public async Task<SmartBpAutoRecognitionTickResult> RunOneTickAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RunOneTickCoreAsync(frame, isDryRun: false, cancellationToken).ConfigureAwait(false);

    public async Task<SmartBpAutoRecognitionTickResult> RunOneTickDryRunAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RunOneTickCoreAsync(frame, isDryRun: true, cancellationToken).ConfigureAwait(false);

    // Strategy execution matrix:
    // PureOcr: full debug OCRs all BP fields; phase-only OCRs status/phase only; automatic uses planner fields.
    // PureAi: full debug is handled by the ViewModel's legacy full BP scan path; phase-only uses AI phase/scene; automatic uses AI phase plus requested field snapshots.
    // AiWithOcr: full debug uses AI phase/scene plus OCR all BP fields; phase-only uses AI phase/scene only; automatic OCRs requested/stale fields.
    // AiWithAiOcr: full debug uses Business AI phase/scene plus AI OCR transcripts for all BP fields; phase-only uses Business AI phase/scene only; automatic uses requested/stale fields.
    public async Task<SmartBpAutoRecognitionTickResult> RunFullRecognitionDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RunOneTickCoreAsync(frame, isDryRun: false, cancellationToken, SmartBpRecognitionDebugMode.FullStrategy).ConfigureAwait(false);

    public async Task<SmartBpAutoRecognitionTickResult> RunIncrementalRecognitionDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RunOneTickCoreAsync(frame, isDryRun: false, cancellationToken, SmartBpRecognitionDebugMode.CurrentStageIncremental).ConfigureAwait(false);

    public async Task<SmartBpAutoRecognitionTickResult> RunPhaseOnlyDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RunPhaseOnlyDebugCoreAsync(frame, cancellationToken).ConfigureAwait(false);

    private async Task<SmartBpAutoRecognitionTickResult> RunOneTickCoreAsync(
        BitmapSource frame,
        bool isDryRun,
        CancellationToken cancellationToken = default,
        SmartBpRecognitionDebugMode debugMode = SmartBpRecognitionDebugMode.Automatic)
    {
        if (!await _tickGate.WaitAsync(0, cancellationToken))
            return Failure("An automatic recognition tick is already running.");
        CancellationTokenSource linked;
        lock (_cancellationLock)
        {
            linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _runCancellation?.Token ?? CancellationToken.None);
            _currentTickCancellation = linked;
        }
        var tickToken = linked.Token;
        var isDebugPreview = debugMode != SmartBpRecognitionDebugMode.Automatic;
        string raw = "";
        try
        {
            var sequence = Interlocked.Increment(ref _frameSequence);
            if (!isDebugPreview && !isDryRun && _hasDetectedPostBp)
                return CreateLatchedPostBpResult(sequence);
            frameRingBuffer.AddFrame(sequence, frame, DateTimeOffset.Now);
            var guidanceSnapshot = guidance.GetRuntimeSnapshot();
            var request = debugMode == SmartBpRecognitionDebugMode.FullStrategy
                ? BuildFullStrategyDebugRequest(stateStore.Snapshot)
                : planner.BuildRequest(guidanceSnapshot, stateStore.Snapshot, ledger.GetSnapshot());
            SmartBpRegionSnapshot? regionSnapshot = null;
            SmartBpPhaseRecognitionResult phaseResult;
            SmartBpCroppedFrame? phaseCrop;
            IReadOnlyList<SmartBpCroppedFrame> contentCrops;
            SmartBpBusinessStateRecognitionResult state;
            var messages = new List<string>(request.Diagnostics);
            if (isDryRun)
                messages.Add("Speed-test dry run: recognition request shape matches automatic tick, but local merge, auto apply, and GameGuidance sync are disabled.");
            if (debugMode == SmartBpRecognitionDebugMode.FullStrategy)
                messages.Add("Full strategy debug: phase_top plus all four BP business fields are requested.");
            else if (debugMode == SmartBpRecognitionDebugMode.CurrentStageIncremental)
                messages.Add("Current-stage incremental debug: automatic planner requested only relevant/stale fields; operation apply and guidance sync are disabled.");
            IReadOnlyDictionary<string, string> rawResponses;
            var recognitionPath = ResolveRecognitionPath(request);
            debugLog.Write("recognition", $"Recognition path={recognitionPath}; structured_output_mode={settings.Settings.StructuredOutputMode}; requested_fields=[{string.Join(", ", request.RequestedFields)}]; legacy_delta={settings.Settings.UseLegacySnapshotDeltaRecognition}.");
            messages.Add($"Recognition path: {recognitionPath}; structured output: {settings.Settings.StructuredOutputMode}.");
            if (!isDebugPreview && !isDryRun)
            {
                var localStatus = await ocrRecognition.RecognizeAsync(
                    frame,
                    new SmartBpOcrRecognitionRequest(
                        [SmartBpRecognitionRegion.TopCenterStatus, SmartBpRecognitionRegion.TopLeftStatus],
                        IncludePhase: false),
                    tickToken).ConfigureAwait(false);
                messages.AddRange(localStatus.Diagnostics);
                var statusRaw = string.Join(Environment.NewLine, localStatus.Regions
                    .Where(region => region.Region is SmartBpRecognitionRegion.TopCenterStatus or SmartBpRecognitionRegion.TopLeftStatus)
                    .SelectMany(region => region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text}")));
                var lifecycle = localStatus.LifecycleStatus;
                if (localStatus.PostBpStatus?.IsPostBp == true || IsPrimaryPostBpPhase(localStatus.Phase.Phase))
                {
                    var aiPhase = "not-run";
                    SmartBpCroppedFrame? comparisonCrop = null;
                    var comparisonRaw = "";
                    if (settings.Settings.RecognitionStrategy != SmartBpRecognitionStrategy.PureOcr)
                    {
                        try
                        {
                            var comparison = await fieldSnapshotRecognition.RecognizePhaseOnlyAsync(frame, tickToken).ConfigureAwait(false);
                            aiPhase = comparison.Phase.Phase;
                            comparisonCrop = comparison.Crop;
                            comparisonRaw = comparison.RawJson;
                            messages.AddRange(comparison.Diagnostics);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            messages.Add($"Business AI phase comparison failed after deterministic post-BP detection: {ex.Message}");
                        }
                    }

                    phaseResult = new SmartBpPhaseRecognitionResult { Phase = localStatus.Phase.Phase };
                    stateStore.ApplyPhase(phaseResult.Phase, sequence);
                    state = stateStore.Snapshot;
                    var statusGate = sceneGate.Classify(phaseResult, state,
                        new Dictionary<string, string> { ["top_left_status"] = statusRaw }, guidanceSnapshot);
                    messages.Add($"TopLeftStatus hard confirmation: {phaseResult.Phase}. AI phase={aiPhase}; final_phase={phaseResult.Phase}.");
                    return LatchAndCreatePostBpPausedResult(
                        sequence, state, phaseResult, comparisonCrop, guidanceSnapshot, messages,
                        string.Join(Environment.NewLine, new[] { statusRaw, comparisonRaw }.Where(value => !string.IsNullOrWhiteSpace(value))), statusGate);
                }

                if (lifecycle != null)
                {
                    messages.Add($"TopCenterStatus lifecycle gate: status={lifecycle.Status}; category={lifecycle.Category}; score={lifecycle.Score:0.00}.");
                    if (lifecycle.Category == SmartBpLifecycleCategory.TransitionToAreaSelection)
                    {
                        _transitionToAreaSelectionConsecutiveCount++;
                        var confirmed = lifecycle.Score >= .80 || _transitionToAreaSelectionConsecutiveCount >= 2;
                        if (confirmed)
                        {
                            phaseResult = new SmartBpPhaseRecognitionResult { Phase = "即将进入区域选择" };
                            stateStore.ApplyPhase(phaseResult.Phase, sequence);
                            state = stateStore.Snapshot;
                            var transitionGate = new SmartBpSceneGateResult(
                                SmartBpRecognitionScene.OutOfBp, false, false, true,
                                "top-center lifecycle reached transition to area selection");
                            messages.Add("content_recognition_allowed=False; post_bp_stop_pending=True; no new BP field recognition will run.");
                            messages.Add($"Post-BP latch set by TopCenterStatus: 即将进入区域选择. confirmation={(lifecycle.Score >= .80 ? "strong score" : "two consecutive weak matches")}.");
                            return LatchAndCreatePostBpPausedResult(sequence, state, phaseResult, null,
                                guidanceSnapshot, messages, statusRaw, transitionGate);
                        }

                        messages.Add("Weak TransitionToAreaSelection match blocked content recognition for this tick; waiting for confirmation; no stop requested.");
                        return CreateLifecycleBlockedResult(sequence, messages, statusRaw,
                            "weak transition match is awaiting a second tick or TopLeftStatus confirmation");
                    }

                    _transitionToAreaSelectionConsecutiveCount = 0;
                    if (lifecycle.IsRecognized)
                    {
                        _lastStableLifecycleCategory = lifecycle.Category;
                        request = FilterAutomaticRequestByLifecycle(request, lifecycle.Category, messages);
                        if ((lifecycle.Category is SmartBpLifecycleCategory.SurvivorTalentAdjust or SmartBpLifecycleCategory.HunterTalentAdjust) &&
                            request.RequestedRegions.Count == 0)
                            return CreateLifecycleBlockedResult(sequence, messages, statusRaw,
                                $"{lifecycle.Category} has no safe configured final-backfill field");
                        messages.Add($"content_recognition_allowed=True; lifecycle_safe_fields=[{string.Join(", ", request.RequestedFields)}].");
                    }
                    else if (_lastStableLifecycleCategory != SmartBpLifecycleCategory.CharacterBpActive)
                    {
                        messages.Add($"TopCenterStatus lifecycle uncertain: best_match={lifecycle.Status} score={lifecycle.Score:0.00}; skipped content recognition for this tick; no stop requested.");
                        return CreateLifecycleBlockedResult(sequence, messages, statusRaw,
                            "top-center lifecycle status is uncertain");
                    }
                    else
                    {
                        messages.Add("TopCenterStatus lifecycle uncertain; safe previous stable CharacterBpActive state allows this tick to continue.");
                    }
                }
            }
            await EnsureStrategyServersAsync(tickToken).ConfigureAwait(false);
            if (!isDebugPreview && !isDryRun &&
                recognitionPath == SmartBpRecognitionPath.LegacyDelta &&
                settings.Settings.RecognitionStrategy != SmartBpRecognitionStrategy.PureOcr)
            {
                var preliminary = await fieldSnapshotRecognition.RecognizePhaseOnlyAsync(frame, tickToken).ConfigureAwait(false);
                messages.AddRange(preliminary.Diagnostics);
                phaseResult = preliminary.Phase;
                phaseCrop = preliminary.Crop;
                stateStore.ApplyPhase(phaseResult.Phase, sequence);
                state = stateStore.Snapshot;
                var preliminaryRaw = new Dictionary<string, string> { ["phase_only"] = preliminary.RawJson };
                var preliminaryGate = sceneGate.Classify(phaseResult, state, preliminaryRaw, guidanceSnapshot);
                messages.Add($"Legacy delta pre-content phase gate: phase={phaseResult.Phase}; scene={preliminaryGate.Scene}; reason={preliminaryGate.Reason}.");
                if (preliminaryGate.ShouldPauseAutomaticRecognition)
                    return LatchAndCreatePostBpPausedResult(sequence, state, phaseResult, phaseCrop,
                        guidanceSnapshot, messages, preliminary.RawJson, preliminaryGate);
                request = FilterAutomaticRequestByPhase(request, phaseResult.Phase, messages);
            }
            if (!isDebugPreview && settings.Settings.RecognitionStrategy == SmartBpRecognitionStrategy.PureOcr)
            {
                var phaseOnlyOcr = await ocrRecognition.RecognizeAsync(
                    frame,
                    new SmartBpOcrRecognitionRequest([], IncludePhase: true),
                    tickToken).ConfigureAwait(false);
                var phaseRaw = string.Join(Environment.NewLine, phaseOnlyOcr.Regions
                    .Where(region => region.Region == SmartBpRecognitionRegion.PhaseTop)
                    .SelectMany(region => region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text}")));
                phaseResult = phaseOnlyOcr.Phase;
                phaseCrop = null;
                if (!isDryRun)
                    stateStore.ApplyPhase(phaseResult.Phase, sequence);
                state = stateStore.Snapshot;
                rawResponses = new Dictionary<string, string> { ["ocr_phase"] = phaseRaw };
                messages.AddRange(phaseOnlyOcr.Diagnostics);
                var preContentGate = sceneGate.Classify(phaseResult, state, rawResponses, guidanceSnapshot);
                messages.Add(preContentGate.ShouldPauseAutomaticRecognition
                    ? $"Post-BP phase detected: phase={phaseResult.Phase}; scene={preContentGate.Scene}."
                    : $"Pure OCR phase gate: phase={phaseResult.Phase}; scene={preContentGate.Scene}.");
                if (preContentGate.ShouldPauseAutomaticRecognition)
                    return LatchAndCreatePostBpPausedResult(sequence,
                        state, phaseResult, phaseCrop, guidanceSnapshot, messages, phaseRaw, preContentGate);
                request = FilterAutomaticRequestByPhase(request, phaseResult.Phase, messages);
                messages.Add("Pure OCR phase gate allowed BP content recognition.");
            }
            if (recognitionPath == SmartBpRecognitionPath.PhaseOnly)
            {
                var phaseOnly = await fieldSnapshotRecognition.RecognizePhaseOnlyAsync(frame, tickToken);
                rawResponses = new Dictionary<string, string> { ["phase_only"] = phaseOnly.RawJson };
                raw = phaseOnly.RawJson;
                messages.AddRange(phaseOnly.Diagnostics);
                phaseResult = phaseOnly.Phase;
                phaseCrop = phaseOnly.Crop;
                contentCrops = [];
                if (!isDryRun)
                {
                    stateStore.ApplyPhase(phaseResult.Phase, sequence);
                    messages.Add($"Applied phase-only update: phase={phaseResult.Phase}.");
                }
            }
            else if (settings.Settings.RecognitionStrategy == SmartBpRecognitionStrategy.AiWithOcr)
            {
                messages.Add($"AI + OCR fusion_mode={settings.Settings.AiWithOcrFusionMode}; default LocalCSharp path uses OCR provider text lines plus local parser/state merge.");
                var phaseOnly = await fieldSnapshotRecognition.RecognizePhaseOnlyAsync(frame, tickToken);
                rawResponses = new Dictionary<string, string> { ["phase_only"] = phaseOnly.RawJson };
                raw = phaseOnly.RawJson;
                messages.AddRange(phaseOnly.Diagnostics);
                phaseResult = phaseOnly.Phase;
                phaseCrop = phaseOnly.Crop;
                if (!isDryRun) stateStore.ApplyPhase(phaseResult.Phase, sequence);
                var preGateState = stateStore.Snapshot;
                var preGate = sceneGate.Classify(phaseResult, preGateState, rawResponses, guidanceSnapshot);
                messages.Add($"AI scene/phase controller: scene={preGate.Scene}; phase={phaseResult.Phase}; allowed={preGate.IsBpRecognitionAllowed}; recommended_fields=[{string.Join(", ", request.RequestedFields)}]; reason={preGate.Reason}.");
                if (!isDebugPreview && preGate.ShouldPauseAutomaticRecognition)
                    return LatchAndCreatePostBpPausedResult(sequence,
                        preGateState, phaseResult, phaseCrop, guidanceSnapshot, messages, raw, preGate);
                if (!isDebugPreview)
                    request = FilterAutomaticRequestByPhase(request, phaseResult.Phase, messages);
                var debugForced = debugMode == SmartBpRecognitionDebugMode.FullStrategy;
                if ((!preGate.IsBpRecognitionAllowed && !debugForced) || request.RequestedRegions.Count == 0)
                {
                    state = preGateState;
                    contentCrops = [];
                    messages.Add(preGate.IsBpRecognitionAllowed
                        ? "AI + OCR skipped OCR because no fields were requested."
                        : "AI + OCR skipped OCR because BP recognition is blocked by the scene decision.");
                }
                else
                {
                    var aiWithOcrTickMode = DescribeRecognitionTickMode(debugMode);
                    var aiWithOcrBeforeMerge = stateStore.Snapshot;
                    messages.Add($"AI + OCR role-distribution diagnostics: saved_frame_id={sequence}; tick_mode={aiWithOcrTickMode}; planner_requested_fields=[{string.Join(", ", request.RequestedFields)}]; ocr_requested_regions=[{string.Join(", ", request.RequestedRegions.Select(item => $"{item.Region}->{item.TargetField}"))}]; debug_thumbnail_id=frame_sequence:{sequence}:ocr_regions.");
                    var ocr = await ocrRecognition.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest(
                        request.RequestedRegions.Select(item => item.Region).Distinct().ToArray(),
                        IncludePhase: false), tickToken);
                    raw = raw + "\n\nocr raw:\n" + string.Join(Environment.NewLine, ocr.Regions.SelectMany(region =>
                        region.Lines.Select(line => $"[{region.Region}] {line.Text} conf={line.Confidence:0.00}")));
                    messages.Add($"AI + OCR role-distribution diagnostics: phase_result={phaseResult.Phase}; ocr_raw_lines=[{string.Join(" | ", ocr.Regions.SelectMany(region => region.Lines.Select(line => $"[{region.Region}] {line.Text}")))}].");
                    messages.Add($"AI + OCR role-distribution diagnostics: parsed_local_state_before_merge={FormatBusinessStateForDiagnostics(ocr.BusinessState)}.");
                    messages.Add($"AI + OCR role-distribution diagnostics: StateStore before merge={FormatBusinessStateForDiagnostics(aiWithOcrBeforeMerge)}.");
                    messages.AddRange(ocr.Diagnostics);
                    var delta = ToDelta(ocr.BusinessState, request.RequestedFields);
                    if (!string.Equals(delta.Phase, phaseResult.Phase, StringComparison.Ordinal))
                    {
                        messages.Add($"AI + OCR local fusion locked final phase to Business AI phase={phaseResult.Phase}; OCR phase={delta.Phase} ignored.");
                        delta.Phase = phaseResult.Phase;
                        ocr.BusinessState.Phase = phaseResult.Phase;
                    }
                    IReadOnlyList<string> mergeDiagnostics = [];
                    if (!isDryRun)
                    {
                        mergeDiagnostics = MergeDeltaWithAutomaticGuards(delta, sequence, phaseResult.Phase, !isDebugPreview);
                        messages.AddRange(mergeDiagnostics);
                    }
                    state = isDryRun ? ocr.BusinessState : stateStore.Snapshot;
                    messages.Add($"AI + OCR role-distribution diagnostics: StateStore after merge={FormatBusinessStateForDiagnostics(state)}.");
                    var staleFieldIgnored = mergeDiagnostics.Any(message => message.Contains("Ignored stale", StringComparison.Ordinal));
                    messages.Add($"AI + OCR role-distribution diagnostics: stale_field_rewritten=false; stale_field_ignored={staleFieldIgnored}; tick_mode={aiWithOcrTickMode}.");
                    contentCrops = [];
                    rawResponses = new Dictionary<string, string>(rawResponses) { ["ocr"] = raw };
                }
            }
            else if (settings.Settings.RecognitionStrategy == SmartBpRecognitionStrategy.AiWithAiOcr)
            {
                var phaseOnly = await fieldSnapshotRecognition.RecognizePhaseOnlyAsync(frame, tickToken);
                var rawMap = new Dictionary<string, string> { ["phase_only"] = phaseOnly.RawJson };
                raw = phaseOnly.RawJson;
                messages.AddRange(phaseOnly.Diagnostics);
                phaseResult = phaseOnly.Phase;
                phaseCrop = phaseOnly.Crop;
                if (!isDryRun) stateStore.ApplyPhase(phaseResult.Phase, sequence);
                var preGateState = stateStore.Snapshot;
                var preGate = sceneGate.Classify(phaseResult, preGateState, rawMap, guidanceSnapshot);
                messages.Add($"AI + AI OCR scene/phase controller: scene={preGate.Scene}; phase={phaseResult.Phase}; allowed={preGate.IsBpRecognitionAllowed}; requested_fields=[{string.Join(", ", request.RequestedFields)}]; reason={preGate.Reason}.");
                if (!isDebugPreview && preGate.ShouldPauseAutomaticRecognition)
                    return LatchAndCreatePostBpPausedResult(sequence,
                        preGateState, phaseResult, phaseCrop, guidanceSnapshot, messages, raw, preGate);
                if (!isDebugPreview)
                    request = FilterAutomaticRequestByPhase(request, phaseResult.Phase, messages);
                var debugForced = debugMode == SmartBpRecognitionDebugMode.FullStrategy;
                if ((!preGate.IsBpRecognitionAllowed && !debugForced) || request.RequestedRegions.Count == 0)
                {
                    state = preGateState;
                    contentCrops = [];
                    rawResponses = rawMap;
                    messages.Add(preGate.IsBpRecognitionAllowed
                        ? "AI + AI OCR skipped transcript extraction because no fields were requested."
                        : "AI + AI OCR skipped transcript extraction because BP recognition is blocked by the scene decision.");
                }
                else
                {
                    var updates = new List<SmartBpSnapshotFieldUpdate>();
                    var evidence = new List<SmartBpAiOcrTranscriptRegionEvidence>();
                    var rawBuilder = new StringBuilder(raw);
                    foreach (var (region, targetField) in request.RequestedRegions)
                    {
                        var transcript = await aiOcrTranscriptRecognition.RecognizeAsync(frame, [(region, targetField)], tickToken);
                        rawMap[$"ai_ocr_{targetField}"] = transcript.RawJson;
                        rawBuilder.Append("\n\n").Append($"ai_ocr_{targetField} raw:\n").Append(transcript.RawJson);
                        messages.AddRange(transcript.Diagnostics);
                        evidence.Add(new()
                        {
                            Region = region,
                            Field = targetField,
                            AiOcrModel = settings.Settings.SelectedAiOcrModelId,
                            RawOutput = transcript.RawJson,
                            TechnicalLines = transcript.Lines.Select(line => line.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray()
                        });
                        if (settings.Settings.AiWithAiOcrFusionMode == SmartBpHybridFusionMode.LocalCSharp)
                        {
                            var interpreted = aiOcrTranscriptInterpreter.Interpret(transcript, region, targetField);
                            updates.Add(interpreted.Update);
                            messages.Add("AI + AI OCR local C# transcript interpreter is experimental.");
                            messages.AddRange(interpreted.Diagnostics);
                        }
                    }

                    SmartBpSnapshotDeltaResult? delta;
                    if (settings.Settings.AiWithAiOcrFusionMode == SmartBpHybridFusionMode.BusinessAi)
                    {
                        try
                        {
                            var outputContract = SmartBpBusinessAiFusionOutputContract.FullBusinessState;
                            messages.Add("pre-fusion raw evidence packaging: AI OCR rawOutput, technicalLines, region/field/model metadata, candidate lists, locked phase, and current known state are sent to Business AI.");
                            var fusion = await businessAiFusion.FuseAsync(phaseResult, evidence, request.RequestedFields, preGateState, outputContract, tickToken);
                            delta = fusion.Delta;
                            rawMap["business_ai_fusion"] = fusion.RawJson;
                            rawBuilder.Append("\n\nbusiness_ai_fusion raw:\n").Append(fusion.RawJson);
                            messages.AddRange(fusion.Diagnostics);
                            messages.Add("AI + AI OCR fusion_mode=BusinessAi; raw AI OCR evidence sent to Business AI");
                            messages.Add($"AI + AI OCR fusion_mode=BusinessAi; output_contract={outputContract}; post-fusion validation completed before merge.");
                        }
                        catch (SmartBpBusinessAiFusionValidationException ex)
                        {
                            delta = null;
                            rawMap["business_ai_fusion"] = ex.RawJson;
                            rawMap["business_ai_fusion_diagnostics"] = string.Join(Environment.NewLine, ex.Diagnostics);
                            rawBuilder.Append("\n\nbusiness_ai_fusion rejected raw:\n").Append(ex.RawJson);
                            rawBuilder.Append("\n\nbusiness_ai_fusion diagnostics:\n").AppendJoin(Environment.NewLine, ex.Diagnostics);
                            messages.AddRange(ex.Diagnostics);
                            messages.Add("Business AI fusion validation failed; corrupted updates were not merged.");
                            messages.Add("Business AI fusion failed after phase/transcript recognition. No final business state was merged.");
                        }
                    }
                    else
                    {
                        delta = new SmartBpSnapshotDeltaResult { Phase = phaseResult.Phase, Updates = updates };
                        messages.Add("AI + AI OCR fusion_mode=LocalCSharp; local transcript interpreter used");
                    }
                    if (!isDryRun && delta != null)
                        messages.AddRange(MergeDeltaWithAutomaticGuards(delta, sequence, phaseResult.Phase, !isDebugPreview));
                    state = isDryRun ? preGateState : stateStore.Snapshot;
                    raw = rawBuilder.ToString();
                    rawResponses = rawMap;
                    contentCrops = [];
                    messages.Add(delta == null
                        ? "AI + AI OCR transcript merge result: rejected; previous StateStore values were preserved."
                        : $"AI + AI OCR transcript merge result updates=[{string.Join(", ", delta.Updates.Select(update => update.Field))}].");
                }
            }
            else if (recognitionPath == SmartBpRecognitionPath.FieldSnapshot || recognitionPath == SmartBpRecognitionPath.FullFieldSnapshot)
            {
                var phaseOnly = await fieldSnapshotRecognition.RecognizePhaseOnlyAsync(frame, tickToken);
                var rawMap = new Dictionary<string, string> { ["phase_only"] = phaseOnly.RawJson };
                raw = phaseOnly.RawJson;
                messages.AddRange(phaseOnly.Diagnostics);
                phaseResult = phaseOnly.Phase;
                phaseCrop = phaseOnly.Crop;
                if (!isDryRun) stateStore.ApplyPhase(phaseResult.Phase, sequence);
                var preGateState = stateStore.Snapshot;
                var preGate = sceneGate.Classify(phaseResult, preGateState, rawMap, guidanceSnapshot);
                messages.Add($"AI field-snapshot scene/phase controller: scene={preGate.Scene}; phase={phaseResult.Phase}; allowed={preGate.IsBpRecognitionAllowed}; requested_fields=[{string.Join(", ", request.RequestedFields)}]; reason={preGate.Reason}.");
                if (!isDebugPreview && preGate.ShouldPauseAutomaticRecognition)
                    return LatchAndCreatePostBpPausedResult(sequence,
                        preGateState, phaseResult, phaseCrop, guidanceSnapshot, messages, raw, preGate);
                if (!isDebugPreview)
                    request = FilterAutomaticRequestByPhase(request, phaseResult.Phase, messages);
                var contentCropList = new List<SmartBpCroppedFrame>();
                var rawBuilder = new StringBuilder(raw);
                foreach (var (region, targetField) in request.RequestedRegions)
                {
                    var fieldResult = await fieldSnapshotRecognition.RecognizeFieldAsync(frame, region, targetField, tickToken);
                    rawMap[$"field_{targetField}"] = fieldResult.RawJson;
                    rawBuilder.Append("\n\n").Append($"field_{targetField} raw:\n").Append(fieldResult.RawJson);
                    messages.AddRange(fieldResult.Diagnostics);
                    contentCropList.Add(fieldResult.Crop);
                    if (isDryRun) continue;
                    var update = new SmartBpSnapshotFieldUpdate { Field = fieldResult.Field, Slots = fieldResult.Slots.ToList(), PickedHun = fieldResult.PickedHun };
                    var mergeMessages = MergeFieldWithAutomaticGuards(
                        fieldResult.Field, update, sequence, phaseResult.Phase, !isDebugPreview);
                    messages.AddRange(mergeMessages);
                }
                rawResponses = rawMap;
                raw = rawBuilder.ToString();
                contentCrops = contentCropList;
            }
            else if (settings.Settings.UseMultiImageSnapshotRequest)
            {
                try
                {
                    var deltaPackage = await deltaRecognition.RecognizeDeltaAsync(frame, request, sequence, tickToken);
                    rawResponses = deltaPackage.RawResponses;
                    raw = string.Join("\n\n", deltaPackage.RawResponses.Select(item => $"{item.Key} raw:\n{item.Value}"));
                    messages.AddRange(deltaPackage.Diagnostics);
                    if (isDryRun)
                        messages.Add("Speed-test dry run: skipped local snapshot delta merge.");
                    else
                        messages.AddRange(MergeDeltaWithAutomaticGuards(
                            deltaPackage.Delta, sequence, deltaPackage.Delta.Phase, !isDebugPreview));
                    phaseResult = new SmartBpPhaseRecognitionResult { Phase = deltaPackage.Delta.Phase };
                    phaseCrop = deltaPackage.PhaseCrop;
                    contentCrops = deltaPackage.ContentCrops;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    messages.Add(settings.Settings.AllowSequentialSnapshotFallback
                        ? $"Multi-image snapshot request failed; falling back to sequential region requests. {ex.Message}"
                        : $"Multi-image snapshot request failed and sequential fallback is disabled. {ex.Message}");
                    if (ex is LlamaCppRequestException requestException) raw = requestException.RawResponse;
                    if (!settings.Settings.AllowSequentialSnapshotFallback)
                        throw;
                    regionSnapshot = await snapshotRecognition.RecognizeSnapshotAsync(frame, SmartBpRegionSnapshotRecognitionMode.PendingAndCurrentRegions, tickToken);
                    rawResponses = regionSnapshot.RawResponses;
                    raw = string.Join("\n\n", regionSnapshot.RawResponses.Select(item => $"{item.Key} raw:\n{item.Value}"));
                    messages.AddRange(regionSnapshot.Diagnostics);
                    var fallbackDelta = ToDelta(regionSnapshot.BusinessState, request.RequestedFields);
                    if (isDryRun)
                        messages.Add("Speed-test dry run: skipped sequential fallback merge.");
                    else
                        messages.AddRange(MergeDeltaWithAutomaticGuards(
                            fallbackDelta, sequence, regionSnapshot.Phase.Phase, !isDebugPreview));
                    phaseResult = regionSnapshot.Phase;
                    phaseCrop = regionSnapshot.PhaseCrop;
                    contentCrops = regionSnapshot.ContentCrops;
                }
            }
            else
            {
                messages.Add("Multi-image snapshot request is disabled; using sequential region fallback.");
                regionSnapshot = await snapshotRecognition.RecognizeSnapshotAsync(frame, SmartBpRegionSnapshotRecognitionMode.PendingAndCurrentRegions, tickToken);
                rawResponses = regionSnapshot.RawResponses;
                raw = string.Join("\n\n", regionSnapshot.RawResponses.Select(item => $"{item.Key} raw:\n{item.Value}"));
                messages.AddRange(regionSnapshot.Diagnostics);
                var fallbackDelta = ToDelta(regionSnapshot.BusinessState, request.RequestedFields);
                if (isDryRun)
                    messages.Add("Speed-test dry run: skipped sequential snapshot merge.");
                else
                    messages.AddRange(MergeDeltaWithAutomaticGuards(
                        fallbackDelta, sequence, regionSnapshot.Phase.Phase, !isDebugPreview));
                phaseResult = regionSnapshot.Phase;
                phaseCrop = regionSnapshot.PhaseCrop;
                contentCrops = regionSnapshot.ContentCrops;
            }

            state = isDryRun && regionSnapshot != null ? regionSnapshot.BusinessState : stateStore.Snapshot;
            guidanceSnapshot = guidance.GetRuntimeSnapshot();
            var gate = sceneGate.Classify(phaseResult, state, rawResponses, guidanceSnapshot);
            messages.Add($"Scene: {gate.Scene}; BP recognition allowed: {gate.IsBpRecognitionAllowed}; Character operations allowed: {gate.IsCharacterOperationAllowed}; Action: {(gate.ShouldPauseAutomaticRecognition ? "automatic recognition paused" : "continue monitoring")}; Reason: {gate.Reason}.");
            if (!isDebugPreview && !isDryRun && gate.ShouldPauseAutomaticRecognition)
                return LatchAndCreatePostBpPausedResult(sequence, state, phaseResult, phaseCrop,
                    guidanceSnapshot, messages, raw, gate);
            ApplyAiUnknownPhaseInference(state, guidanceSnapshot, gate, messages);
            if (isDryRun)
            {
                var dryRunSync = new SmartBpGuidanceSyncResult(false, false, "Speed-test dry run: GameGuidance synchronization is disabled.", null, [], null);
                var dryRunApply = new SmartBpOperationApplyResult(0, 0, ["Speed-test dry run: character operation application is disabled."]);
                var dryRunSnapshot = regionSnapshot ?? new SmartBpRegionSnapshot
                {
                    Phase = phaseResult,
                    BusinessState = state,
                    Diagnostics = messages,
                    PhaseCrop = phaseCrop,
                    ContentCrops = contentCrops,
                    RawResponses = new Dictionary<string, string> { ["snapshot_delta"] = raw }
                };
                return new(state, phaseResult, null, phaseCrop, null, dryRunSync, guidanceSnapshot,
                    [], messages, dryRunApply, raw, null, dryRunSnapshot, null, contentCrops, gate);
            }

            var isFreeSync = settings.Settings.RecognitionApplyMode == SmartBpRecognitionApplyMode.FreeFullSync;
            var plan = !gate.IsCharacterOperationAllowed
                ? new SmartBpWorkflowBackfillPlan([], [$"Character operations blocked by scene gate: {gate.Reason}."])
                : isFreeSync
                ? new SmartBpWorkflowBackfillPlan([], ["Free full sync bypasses GameGuidance workflow planning."])
                : backfill.BuildPlan(state, guidanceSnapshot);
            var operations = !gate.IsCharacterOperationAllowed
                ? []
                : isFreeSync
                ? BuildFreeSyncOperations(state, candidateBuilder)
                : plan.StepCandidates.SelectMany(item => item.Operations).ToArray();
            messages.AddRange(plan.Diagnostics);
            messages.AddRange(plan.StepCandidates.Select(item => $"Step {item.StepIndex} {item.Action} [{string.Join(",", item.Indexes)}]: {item.Reason} Candidates={item.Operations.Count}."));
            var fingerprint = JsonSerializer.Serialize(state);
            _stableSnapshotCount = string.Equals(_lastSnapshotFingerprint, fingerprint, StringComparison.Ordinal)
                ? _stableSnapshotCount + 1
                : 1;
            _lastSnapshotFingerprint = fingerprint;
            var requiredStable = Math.Max(1, settings.Settings.RequiredStableSnapshots);
            SmartBpOperationApplyResult applyResult = isDebugPreview
                ? new(0, operations.Length, ["Recognition debug preview: operation application is disabled."])
                : settings.Settings.EnableAutoApplyRecognition && _stableSnapshotCount >= requiredStable
                ? await applier.ApplyAsync(operations, tickToken)
                : settings.Settings.EnableAutoApplyRecognition
                    ? new(0, operations.Length, [$"Skipped: waiting for stable BP snapshots ({_stableSnapshotCount}/{requiredStable})."])
                    : new(0, operations.Length, operations.Length == 0
                    ? ["Skipped: auto apply disabled; no candidate operations were generated."]
                    : operations.Select(x => $"Skipped: auto apply disabled for step {x.SourceWorkflowStepIndex} {x.Kind} {x.Camp}[{x.SlotIndex}] {x.RawCharacterName ?? "null"}.").ToArray());
            if (settings.Settings.EnableAutoGuidanceSync &&
                SmartBpAutomaticMapping.TryMapPhase(state.Phase, out var detectedAction) &&
                guidanceSnapshot.CurrentAction != null &&
                guidanceSnapshot.CurrentAction != detectedAction &&
                operations.Length > 0)
            {
                var hold = Math.Min(settings.Settings.PhaseTransitionCommitHoldMilliseconds, settings.Settings.PhaseTransitionCommitHoldMaxMilliseconds);
                if (hold > 0)
                {
                    messages.Add($"Transition commit hold {hold}ms before guidance sync; pending operations were processed first.");
                    await Task.Delay(hold, tickToken);
                }
            }
            var delayedAiCanSync = settings.Settings.RecognitionStrategy == SmartBpRecognitionStrategy.PureOcr ||
                                   !settings.Settings.AiOneStepDelayedMode ||
                                   operations.All(IsOperationCompleted);
            SmartBpGuidanceSyncResult? sync = !isDebugPreview && gate.IsBpRecognitionAllowed && !isFreeSync && settings.Settings.EnableAutoGuidanceSync && delayedAiCanSync
                ? await guidanceSync.SyncAsync(state, tickToken)
                : new(false, false, isDebugPreview
                    ? "Recognition debug preview: GameGuidance synchronization is disabled."
                    : isFreeSync
                    ? "Free full sync does not synchronize GameGuidance."
                    : !delayedAiCanSync
                        ? "AI delayed mode is waiting for current or previous operations to be applied or confirmed as no-op."
                        : "Automatic GameGuidance synchronization is disabled.", null, [], null);
            var finalGuidanceSnapshot = guidance.GetRuntimeSnapshot();
            var snapshotForUi = regionSnapshot ?? new SmartBpRegionSnapshot
            {
                Phase = phaseResult,
                BusinessState = state,
                Diagnostics = messages,
                PhaseCrop = phaseCrop,
                ContentCrops = contentCrops,
                RawResponses = new Dictionary<string, string> { ["snapshot_delta"] = raw }
            };
            return new(state, phaseResult, null, phaseCrop, null, sync, finalGuidanceSnapshot,
                operations, messages, applyResult, raw, null, snapshotForUi, plan, contentCrops, gate);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (ex is LlamaCppRequestException request) raw = request.RawResponse;
            return new(null, null, null, null, null, null, guidance.GetRuntimeSnapshot(), [], [], null, raw, ex.Message);
        }
        finally
        {
            lock (_cancellationLock)
            {
                if (ReferenceEquals(_currentTickCancellation, linked))
                    _currentTickCancellation = null;
            }
            linked.Dispose();
            _tickGate.Release();
        }
    }

    private SmartBpAutoRecognitionTickResult Failure(string error) =>
        new(null, null, null, null, null, null, guidance.GetRuntimeSnapshot(), [], [], null, "", error);

    private SmartBpAutoRecognitionTickResult LatchAndCreatePostBpPausedResult(
        long frameSequence,
        SmartBpBusinessStateRecognitionResult state,
        SmartBpPhaseRecognitionResult phaseResult,
        SmartBpCroppedFrame? phaseCrop,
        GameGuidanceRuntimeSnapshot guidanceSnapshot,
        ICollection<string> messages,
        string raw,
        SmartBpSceneGateResult gate)
    {
        if (!_hasDetectedPostBp)
        {
            _hasDetectedPostBp = true;
            _postBpPhase = phaseResult.Phase;
            _postBpDetectedFrameSequence = frameSequence;
            messages.Add($"Post-BP latch set: phase={_postBpPhase}; frame_sequence={_postBpDetectedFrameSequence}.");
        }
        return CreatePostBpPausedResult(state, phaseResult, phaseCrop, guidanceSnapshot, messages, raw, gate);
    }

    private SmartBpAutoRecognitionTickResult CreateLatchedPostBpResult(long frameSequence)
    {
        var phase = new SmartBpPhaseRecognitionResult { Phase = _postBpPhase };
        var state = stateStore.Snapshot;
        var messages = new List<string>
        {
            $"Post-BP latch already set at frame_sequence={_postBpDetectedFrameSequence}; ignored recognition tick frame_sequence={frameSequence}.",
            "No BP content recognition or field merge was run."
        };
        var scene = _postBpPhase switch
        {
            "求生者选择区域中" => SmartBpRecognitionScene.AreaSelectionSurvivor,
            "监管者选择区域中" => SmartBpRecognitionScene.AreaSelectionHunter,
            "等待游戏开始" => SmartBpRecognitionScene.WaitingGameStart,
            _ => SmartBpRecognitionScene.OutOfBp
        };
        var gate = new SmartBpSceneGateResult(scene, false, false, true,
            "post-BP latch prevents recognition from resuming before automatic stop completes");
        return CreatePostBpPausedResult(state, phase, null, guidance.GetRuntimeSnapshot(), messages, "", gate);
    }

    private void ClearPostBpLatch()
    {
        _hasDetectedPostBp = false;
        _postBpPhase = "未知";
        _postBpDetectedFrameSequence = 0;
        _transitionToAreaSelectionConsecutiveCount = 0;
        _lastStableLifecycleCategory = SmartBpLifecycleCategory.Unknown;
    }

    private SmartBpAutoRecognitionTickResult CreateLifecycleBlockedResult(
        long frameSequence,
        ICollection<string> messages,
        string raw,
        string reason)
    {
        var state = stateStore.Snapshot;
        var phase = new SmartBpPhaseRecognitionResult { Phase = state.Phase };
        var gate = new SmartBpSceneGateResult(SmartBpRecognitionScene.Unknown, false, false, false, reason);
        messages.Add("content_recognition_allowed=False; no BP OCR, AI OCR, Business AI fusion, field merge, or candidate operation generation was run.");
        return new(state, phase, null, null, null, null, guidance.GetRuntimeSnapshot(), [], messages.ToArray(),
            new SmartBpOperationApplyResult(0, 0, []), raw, null, null, null, [], gate);
    }

    private static bool IsPrimaryPostBpPhase(string phase) =>
        phase is "求生者选择区域中" or "监管者选择区域中" or "等待游戏开始";

    internal static SmartBpSnapshotDeltaRequest FilterAutomaticRequestByPhase(
        SmartBpSnapshotDeltaRequest request,
        string authoritativePhase,
        ICollection<string>? diagnostics = null)
    {
        var allowedFields = authoritativePhase switch
        {
            "屏蔽求生者" => new HashSet<string>(["banned_sur"], StringComparer.Ordinal),
            "屏蔽监管者" => new HashSet<string>(["banned_hun"], StringComparer.Ordinal),
            "选择求生者" or "求生者选择角色中" or "求生者选择天赋中" =>
                new HashSet<string>(["picked_sur"], StringComparer.Ordinal),
            "选择监管者" => new HashSet<string>(["picked_hun"], StringComparer.Ordinal),
            _ => new HashSet<string>(StringComparer.Ordinal)
        };
        var filtered = request.RequestedRegions
            .Where(item => allowedFields.Contains(item.TargetField))
            .ToArray();
        var removed = request.RequestedFields.Where(field => !allowedFields.Contains(field)).ToArray();
        if (removed.Length > 0)
            diagnostics?.Add($"Phase-aware field filter removed [{string.Join(", ", removed)}] because authoritative phase={authoritativePhase}.");
        if (filtered.Length == 0)
            diagnostics?.Add($"Phase-aware field filter requested no content fields for authoritative phase={authoritativePhase}.");
        return new SmartBpSnapshotDeltaRequest(filtered, request.Diagnostics, request.CurrentKnownState);
    }

    internal static SmartBpSnapshotDeltaRequest FilterAutomaticRequestByLifecycle(
        SmartBpSnapshotDeltaRequest request,
        SmartBpLifecycleCategory category,
        ICollection<string>? diagnostics = null)
    {
        HashSet<string>? allowedFields = category switch
        {
            SmartBpLifecycleCategory.CharacterBpActive => null,
            SmartBpLifecycleCategory.SurvivorTalentAdjust => new(["picked_sur"], StringComparer.Ordinal),
            SmartBpLifecycleCategory.HunterTalentAdjust => new(["picked_hun"], StringComparer.Ordinal),
            _ => new(StringComparer.Ordinal)
        };
        if (allowedFields == null) return request;
        var filtered = request.RequestedRegions.Where(item => allowedFields.Contains(item.TargetField)).ToArray();
        var removed = request.RequestedFields.Where(field => !allowedFields.Contains(field)).ToArray();
        if (removed.Length > 0)
            diagnostics?.Add($"Lifecycle-aware field filter removed [{string.Join(", ", removed)}] because category={category}.");
        return new SmartBpSnapshotDeltaRequest(filtered, request.Diagnostics, request.CurrentKnownState);
    }

    private IReadOnlyList<string> MergeDeltaWithAutomaticGuards(
        SmartBpSnapshotDeltaResult delta,
        long frameSequence,
        string authoritativePhase,
        bool enforceAutomaticGuards)
    {
        if (!enforceAutomaticGuards)
            return stateStore.ApplyDelta(delta, frameSequence, DateTimeOffset.Now);
        if (_hasDetectedPostBp)
            return [$"Ignored all BP field updates because post-BP latch phase={_postBpPhase} is set."];

        var request = new SmartBpSnapshotDeltaRequest(
            delta.Updates.Select(update => (RegionForField(update.Field), update.Field)).ToArray(),
            []);
        var allowed = FilterAutomaticRequestByPhase(request, authoritativePhase).RequestedFields.ToHashSet(StringComparer.Ordinal);
        var diagnostics = delta.Updates
            .Where(update => !allowed.Contains(update.Field))
            .Select(update => update.Field == "picked_hun"
                ? $"Ignored picked_hun update because authoritative phase={authoritativePhase} does not allow hunter pick updates."
                : $"Ignored {update.Field} update because authoritative phase={authoritativePhase} does not allow that field.")
            .ToList();
        var guarded = new SmartBpSnapshotDeltaResult
        {
            Phase = authoritativePhase,
            Updates = delta.Updates.Where(update => allowed.Contains(update.Field)).ToList()
        };
        diagnostics.AddRange(stateStore.ApplyDelta(guarded, frameSequence, DateTimeOffset.Now));
        return diagnostics;
    }

    private IReadOnlyList<string> MergeFieldWithAutomaticGuards(
        string field,
        SmartBpSnapshotFieldUpdate update,
        long frameSequence,
        string authoritativePhase,
        bool enforceAutomaticGuards)
    {
        if (!enforceAutomaticGuards)
            return stateStore.ApplyFieldSnapshot(field, update, frameSequence, DateTimeOffset.Now);
        if (_hasDetectedPostBp)
            return [$"Ignored {field} update because post-BP latch phase={_postBpPhase} is set."];
        var request = new SmartBpSnapshotDeltaRequest([(RegionForField(field), field)], []);
        if (FilterAutomaticRequestByPhase(request, authoritativePhase).RequestedFields.Contains(field, StringComparer.Ordinal))
            return stateStore.ApplyFieldSnapshot(field, update, frameSequence, DateTimeOffset.Now);
        return
        [
            field == "picked_hun"
                ? $"Ignored picked_hun update because authoritative phase={authoritativePhase} does not allow hunter pick updates."
                : $"Ignored {field} update because authoritative phase={authoritativePhase} does not allow that field."
        ];
    }

    private static SmartBpRecognitionRegion RegionForField(string field) => field switch
    {
        "banned_sur" => SmartBpRecognitionRegion.RightTop,
        "banned_hun" => SmartBpRecognitionRegion.LeftTop,
        "picked_sur" => SmartBpRecognitionRegion.LeftBottom,
        "picked_hun" => SmartBpRecognitionRegion.RightBottom,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown SmartBP field.")
    };

    private static SmartBpAutoRecognitionTickResult CreatePostBpPausedResult(
        SmartBpBusinessStateRecognitionResult state,
        SmartBpPhaseRecognitionResult phaseResult,
        SmartBpCroppedFrame? phaseCrop,
        GameGuidanceRuntimeSnapshot guidanceSnapshot,
        ICollection<string> messages,
        string raw,
        SmartBpSceneGateResult gate)
    {
        messages.Add($"Post-BP phase detected: phase={phaseResult.Phase}; scene={gate.Scene}.");
        messages.Add("Character BP has ended; no new recognition ticks will be scheduled.");
        messages.Add("Automatic recognition stop is queued after pending operations drain.");
        messages.Add("Skipped content field recognition because BP ended.");
        return new(state, phaseResult, null, phaseCrop, null, null, guidanceSnapshot,
            [], messages.ToArray(), new(0, 0, ["Skipped: BP ended before content recognition; no character operations were generated."]),
            raw, null, null, null, [], gate);
    }

    private async Task<SmartBpAutoRecognitionTickResult> RunPhaseOnlyDebugCoreAsync(
        BitmapSource frame,
        CancellationToken cancellationToken = default)
    {
        if (!await _tickGate.WaitAsync(0, cancellationToken))
            return Failure("An automatic recognition tick is already running.");
        try
        {
            if (settings.Settings.RecognitionStrategy != SmartBpRecognitionStrategy.PureOcr)
                await StartIfNeededAsync(llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi), cancellationToken).ConfigureAwait(false);
            SmartBpPhaseRecognitionResult phaseResult;
            SmartBpCroppedFrame? phaseCrop;
            string raw;
            IReadOnlyDictionary<string, string> rawResponses;
            var messages = new List<string>
            {
                $"Phase-only debug: strategy={settings.Settings.RecognitionStrategy}; no field OCR, AI OCR, merge, operations, or apply."
            };

            if (settings.Settings.RecognitionStrategy == SmartBpRecognitionStrategy.PureOcr)
            {
                var ocr = await ocrRecognition.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest([], IncludePhase: true), cancellationToken).ConfigureAwait(false);
                phaseResult = ocr.Phase;
                phaseCrop = null;
                raw = string.Join(Environment.NewLine, ocr.Regions.SelectMany(region => region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text}")));
                rawResponses = new Dictionary<string, string> { ["ocr_phase"] = raw };
                messages.AddRange(ocr.Diagnostics);
            }
            else
            {
                var phaseOnly = await fieldSnapshotRecognition.RecognizePhaseOnlyAsync(frame, cancellationToken).ConfigureAwait(false);
                phaseResult = phaseOnly.Phase;
                phaseCrop = phaseOnly.Crop;
                raw = phaseOnly.RawJson;
                rawResponses = new Dictionary<string, string> { ["phase_only"] = raw };
                messages.AddRange(phaseOnly.Diagnostics);
            }

            var state = stateStore.Snapshot;
            var gate = sceneGate.Classify(phaseResult, state, rawResponses, guidance.GetRuntimeSnapshot());
            messages.Add($"Scene: {gate.Scene}; BP recognition allowed: {gate.IsBpRecognitionAllowed}; Character operations allowed: {gate.IsCharacterOperationAllowed}; Action: {(gate.ShouldPauseAutomaticRecognition ? "automatic recognition paused" : "continue monitoring")}; Reason: {gate.Reason}.");
            return new(state, phaseResult, null, phaseCrop, null, null, guidance.GetRuntimeSnapshot(),
                [], messages, new(0, 0, ["Phase-only debug: operation generation is disabled."]), raw, null, null, null, [], gate);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(null, null, null, null, null, null, guidance.GetRuntimeSnapshot(), [], [], null, "", ex.Message);
        }
        finally
        {
            _tickGate.Release();
        }
    }

    private static SmartBpSnapshotDeltaRequest BuildFullStrategyDebugRequest(SmartBpBusinessStateRecognitionResult currentKnownState) =>
        new(
            [
                (SmartBpRecognitionRegion.RightTop, "banned_sur"),
                (SmartBpRecognitionRegion.LeftTop, "banned_hun"),
                (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
                (SmartBpRecognitionRegion.RightBottom, "picked_hun")
            ],
            ["Full strategy debug requested phase_top and all four BP business fields."],
            currentKnownState);

    /// <summary>
    /// Resolves which recognition path the coordinator should use for this tick.
    /// OCR engine and legacy delta flag always use the legacy delta path. AI engine without
    /// legacy flag uses phase-only when no fields are requested, field snapshot when one or more
    /// fields are requested, and full field snapshot when all four fields are requested.
    /// </summary>
    /// <param name="request">The planner-built recognition request.</param>
    /// <returns>The recognition path enum value.</returns>
    private SmartBpRecognitionPath ResolveRecognitionPath(SmartBpSnapshotDeltaRequest request)
    {
        if (settings.Settings.RecognitionStrategy == SmartBpRecognitionStrategy.PureOcr && settings.Settings.EnableOcrBpRecognition)
            return SmartBpRecognitionPath.LegacyDelta;
        if (settings.Settings.RecognitionStrategy == SmartBpRecognitionStrategy.AiWithOcr)
            return request.RequestedFields.Count == 0 ? SmartBpRecognitionPath.PhaseOnly : SmartBpRecognitionPath.FieldSnapshot;
        if (settings.Settings.UseLegacySnapshotDeltaRecognition)
            return SmartBpRecognitionPath.LegacyDelta;
        var requestedFields = request.RequestedFields;
        if (requestedFields.Count == 0)
            return SmartBpRecognitionPath.PhaseOnly;
        var allFields = new HashSet<string>(StringComparer.Ordinal) { "banned_sur", "banned_hun", "picked_sur", "picked_hun" };
        return allFields.IsSubsetOf(requestedFields) ? SmartBpRecognitionPath.FullFieldSnapshot : SmartBpRecognitionPath.FieldSnapshot;
    }

    private async Task EnsureStrategyServersAsync(CancellationToken cancellationToken)
    {
        switch (settings.Settings.RecognitionStrategy)
        {
            case SmartBpRecognitionStrategy.PureOcr:
                return;
            case SmartBpRecognitionStrategy.PureAi:
            case SmartBpRecognitionStrategy.AiWithOcr:
                await StartIfNeededAsync(llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi), cancellationToken).ConfigureAwait(false);
                return;
            case SmartBpRecognitionStrategy.AiWithAiOcr:
                var business = llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi);
                await StartIfNeededAsync(business, cancellationToken).ConfigureAwait(false);
                if (!settings.Settings.UseSeparateAiOcrServer ||
                    string.Equals(settings.Settings.SelectedBusinessAiModelId, settings.Settings.SelectedAiOcrModelId, StringComparison.Ordinal))
                {
                    debugLog.Write("llama-server", "AI OCR is reusing the Business AI server because the selected model is the same or separate server mode is disabled.");
                    return;
                }
                await StartIfNeededAsync(llamaServerManagers.Get(LlamaVisionServerRole.AiOcr), cancellationToken).ConfigureAwait(false);
                return;
        }
    }

    private static async Task StartIfNeededAsync(ILlamaCppServerManager manager, CancellationToken cancellationToken)
    {
        if (!manager.IsRunning)
            await manager.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SmartBpDetectedOperation[] BuildFreeSyncOperations(
        SmartBpBusinessStateRecognitionResult state,
        SmartBpCandidateOperationBuilder builder)
    {
        return new[]
        {
            builder.BuildWithDiagnostics(state, GameAction.BanSur, []).Operations,
            builder.BuildWithDiagnostics(state, GameAction.BanHun, []).Operations,
            builder.BuildWithDiagnostics(state, GameAction.PickSur, []).Operations,
            builder.BuildWithDiagnostics(state, GameAction.PickHun, []).Operations
        }.SelectMany(items => items)
         .Select(operation => operation with { ApplyMode = SmartBpDetectedOperationApplyMode.FreeSync, SourceWorkflowStepIndex = null })
         .ToArray();
    }

    private bool IsOperationCompleted(SmartBpDetectedOperation operation)
    {
        var key = SmartBpWorkflowBackfillService.CreateKey(shared.CurrentGame.GameProgress, operation);
        return key is not null && ledger.IsStepOperationCompleted(key);
    }

    private void ApplyAiUnknownPhaseInference(
        SmartBpBusinessStateRecognitionResult state,
        GameGuidanceRuntimeSnapshot guidanceSnapshot,
        SmartBpSceneGateResult gate,
        ICollection<string> diagnostics)
    {
        if (settings.Settings.RecognitionStrategy == SmartBpRecognitionStrategy.PureOcr ||
            !settings.Settings.AiOneStepDelayedMode || !guidanceSnapshot.IsStarted ||
            gate.Scene is not (SmartBpRecognitionScene.CharacterBp or SmartBpRecognitionScene.HunterTalent))
        {
            _unknownPhaseFrames = 0;
            return;
        }
        if (SmartBpAutomaticMapping.TryMapPhase(state.Phase, out var explicitAction))
        {
            _lastExplicitAction = explicitAction;
            _unknownPhaseFrames = 0;
            return;
        }
        if (!string.Equals(state.Phase, "未知", StringComparison.Ordinal) || _lastExplicitAction != GameAction.PickHun)
        {
            _unknownPhaseFrames = 0;
            return;
        }
        _unknownPhaseFrames++;
        if (_unknownPhaseFrames < Math.Max(1, settings.Settings.AiUnknownPhaseTalentInferenceFrames)) return;
        var pickStep = guidanceSnapshot.Workflow.Where(step => step.Action == GameAction.PickHun && step.StepIndex <= guidanceSnapshot.CurrentStepIndex)
            .OrderByDescending(step => step.StepIndex).FirstOrDefault();
        var future = guidanceSnapshot.Workflow.Where(step => step.StepIndex > guidanceSnapshot.CurrentStepIndex)
            .OrderBy(step => step.StepIndex).FirstOrDefault(step => step.Action == GameAction.PickHunTalent);
        var character = state.PickedHun.CharacterName;
        if (pickStep is null || future is null || SmartBpBusinessStateParser.IsUnselected(character)) return;
        var key = new SmartBpWorkflowOperationKey(
            shared.CurrentGame.GameProgress, pickStep.StepIndex, GameAction.PickHun, -1, Camp.Hun, character);
        if (!ledger.IsStepOperationCompleted(key)) return;
        state.Phase = "监管者选择天赋中";
        diagnostics.Add("AI delayed mode inferred hunter talent selection after a completed hunter pick.");
        _unknownPhaseFrames = 0;
    }

    public async Task<SmartBpStepCommitResult> ProcessTickAsync(BitmapSource frame, CancellationToken cancellationToken = default)
    {
        var result = await RunOneTickAsync(frame, cancellationToken).ConfigureAwait(false);
        if (result.BusinessState == null || result.BackfillPlan == null)
            throw new InvalidOperationException(result.Error ?? "SmartBP step commit tick failed.");
        return new(result.BusinessState, result.BackfillPlan, result.ApplyResult, result.GuidanceSync, result.CandidateMessages);
    }

    private static string DescribeRecognitionTickMode(SmartBpRecognitionDebugMode debugMode) =>
        debugMode switch
        {
            SmartBpRecognitionDebugMode.FullStrategy => "full image debug",
            SmartBpRecognitionDebugMode.CurrentStageIncremental => "incremental debug",
            _ => "automatic"
        };

    private static string FormatBusinessStateForDiagnostics(SmartBpBusinessStateRecognitionResult state) =>
        JsonSerializer.Serialize(SmartBpRecognitionPromptBuilder.CreateCurrentKnownStateJson(state));

    private static SmartBpSnapshotDeltaResult ToDelta(SmartBpBusinessStateRecognitionResult state, IReadOnlyCollection<string> requestedFields)
    {
        var updates = new List<SmartBpSnapshotFieldUpdate>();
        if (requestedFields.Contains("banned_sur"))
            updates.Add(new() { Field = "banned_sur", Slots = state.BannedSur.Select(ToDeltaSlot).ToList() });
        if (requestedFields.Contains("banned_hun"))
            updates.Add(new() { Field = "banned_hun", Slots = state.BannedHun.Select(ToDeltaSlot).ToList() });
        if (requestedFields.Contains("picked_sur"))
            updates.Add(new() { Field = "picked_sur", Slots = state.PickedSur.Select(ToDeltaSlot).ToList() });
        if (requestedFields.Contains("picked_hun"))
            updates.Add(new() { Field = "picked_hun", PickedHun = ToDeltaSlot(state.PickedHun) });
        return new() { Phase = state.Phase, Updates = updates };
    }

    private static SmartBpSnapshotDeltaSlot ToDeltaSlot(SmartBpRecognizedCharacterSlot slot) =>
        new()
        {
            Index = slot.Index,
            SlotState = SmartBpBusinessStateParser.IsUnselected(slot.CharacterName) ? "empty" : "selected",
            CharacterName = slot.CharacterName
        };

    private static SmartBpSnapshotDeltaSlot ToDeltaSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new()
        {
            Index = slot.Index,
            SlotState = SmartBpBusinessStateParser.IsUnselected(slot.CharacterName) ? "empty" : "selected",
            CharacterName = slot.CharacterName,
            PlayerId = slot.PlayerId
        };
}
