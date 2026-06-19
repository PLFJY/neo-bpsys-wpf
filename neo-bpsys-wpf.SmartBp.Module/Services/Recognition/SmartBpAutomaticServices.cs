using System.IO;
using System.Text;
using System.Text.Json;
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
        "求生者选择天赋中", "监管者选择天赋中", "天赋已锁定", "等待中", "未知"
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
        var result = JsonSerializer.Deserialize<SmartBpSnapshotDeltaResult>(raw)
            ?? throw new InvalidDataException("Snapshot delta JSON is empty.");
        if (!SmartBpAutomaticMapping.ValidPhases.Contains(result.Phase)) throw new InvalidDataException("Invalid BP phase.");
        result.Updates ??= [];
        var requested = requestedFields.ToHashSet(StringComparer.Ordinal);
        foreach (var update in result.Updates)
        {
            if (!requested.Contains(update.Field)) throw new InvalidDataException($"Snapshot delta contained an unrequested field: {update.Field}.");
            switch (update.Field)
            {
                case "banned_sur":
                    ValidateDeltaSlots(update.Slots, 4, survivorCandidates, update.Field);
                    if (update.PickedHun != null) throw new InvalidDataException("banned_sur update must not contain picked_hun.");
                    break;
                case "banned_hun":
                    ValidateDeltaSlots(update.Slots, 2, hunterCandidates, update.Field);
                    if (update.PickedHun != null) throw new InvalidDataException("banned_hun update must not contain picked_hun.");
                    break;
                case "picked_sur":
                    ValidateDeltaSlots(update.Slots, 4, survivorCandidates, update.Field);
                    if (update.PickedHun != null) throw new InvalidDataException("picked_sur update must not contain picked_hun.");
                    break;
                case "picked_hun":
                    if (update.Slots != null) throw new InvalidDataException("picked_hun update must not contain slots.");
                    if (update.PickedHun == null) throw new InvalidDataException("picked_hun update must contain picked_hun.");
                    if (update.PickedHun.Index != 0) throw new InvalidDataException("picked_hun.index must be 0.");
                    NormalizeFocusedSlot(update.PickedHun, hunterCandidates, "picked_hun");
                    break;
                default:
                    throw new InvalidDataException($"Invalid snapshot delta field: {update.Field}.");
            }
        }
        return result;
    }

    private static void ValidateDeltaSlots(List<SmartBpRecognizedPlayerCharacterSlot>? slots, int count, IReadOnlyCollection<string> allowed, string field)
    {
        if (slots == null) throw new InvalidDataException($"{field} update must contain slots.");
        if (slots.Count != count) throw new InvalidDataException($"{field}.slots must contain exactly {count} entries.");
        var expectedIndexes = Enumerable.Range(0, count).ToArray();
        if (!slots.Select(x => x.Index).OrderBy(x => x).SequenceEqual(expectedIndexes))
            throw new InvalidDataException($"{field}.slots contain invalid indexes.");
        foreach (var slot in slots) NormalizeFocusedSlot(slot, allowed, field);
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
                state.BannedSur = focused.Slots.Select(x => new SmartBpRecognizedCharacterSlot { Index = x.Index, CharacterName = x.CharacterName }).ToList();
                break;
            case "banned_hun":
                state.BannedHun = focused.Slots.Select(x => new SmartBpRecognizedCharacterSlot { Index = x.Index, CharacterName = x.CharacterName }).ToList();
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
            var error = await guidance.StartGuidance();
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
        var moveError = await guidance.MoveToStepAsync(target.StepIndex);
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
        var moveError = await guidance.MoveToStepAsync(target.StepIndex);
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
            var resolved = resolver.Resolve(slot.CharacterName, camp, slot.Index, 1);
            operations.Add(new(kind, action, guidanceIndexes.ToArray(), camp, slot.Index, slot.CharacterName,
                resolved.ResolvedCharacterKey, resolved.ResolvedCharacterName, null, 1,
                $"Business-state snapshot phase {action} produced slot {slot.Index}."));
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
            var resolved = resolver.Resolve(slot.CharacterName, camp, internalSlot, 1);
            operations.Add(new(kind, action, guidanceIndexes.ToArray(), camp, internalSlot, slot.CharacterName,
                resolved.ResolvedCharacterKey, resolved.ResolvedCharacterName, slot.PlayerId, 1,
                hunterSlot ? "Business-state snapshot mapped hunter visual slot 0 to internal hunter slot -1." : $"Business-state snapshot phase {action} produced slot {internalSlot}."));
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
            var resolved = resolver.Resolve(slot.CharacterName, Camp.Sur, slot.Index, 1);
            if (resolved.ResolvedCharacterName != null && simulated[slot.Index] == resolved.ResolvedCharacterName)
            {
                messages.Add($"Skipped: no-op same character Sur[{slot.Index}] {slot.CharacterName}.");
                continue;
            }
            operations.Add(new(SmartBpDetectedOperationKind.SwapSurvivors, GameAction.DistributeChara,
                guidanceIndexes.ToArray(), Camp.Sur, slot.Index, slot.CharacterName,
                resolved.ResolvedCharacterKey, resolved.ResolvedCharacterName, slot.PlayerId,
                1, $"Place the detected character into fixed survivor player slot {slot.Index}."));
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
            var playAnimation = operation.ApplyMode == SmartBpDetectedOperationApplyMode.CurrentStep || settings.Settings.PlayBackfillAnimations;
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

internal sealed class SmartBpAutoRecognitionCoordinator(
    ISmartBpRegionSnapshotRecognitionService snapshotRecognition,
    ISmartBpSnapshotDeltaRecognitionService deltaRecognition,
    ISmartBpSnapshotRecognitionPlanner planner,
    ISmartBpRecognitionStateStore stateStore,
    ISmartBpRecognitionLedger ledger,
    ISmartBpFrameRingBuffer frameRingBuffer,
    ISmartBpRecognitionSettingsService settings,
    ISmartBpGuidanceSyncService guidanceSync,
    IGameGuidanceService guidance,
    ISmartBpWorkflowBackfillService backfill,
    ISmartBpDetectedOperationApplier applier) : ISmartBpAutoRecognitionCoordinator, ISmartBpStepCommitScheduler
{
    private readonly SemaphoreSlim _tickGate = new(1, 1);
    private CancellationTokenSource? _runCancellation;
    private string? _lastSnapshotFingerprint;
    private int _stableSnapshotCount;
    private long _frameSequence;
    public bool IsRunning => _runCancellation is { IsCancellationRequested: false };

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _runCancellation?.Cancel();
        _runCancellation?.Dispose();
        _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _lastSnapshotFingerprint = null;
        _stableSnapshotCount = 0;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _runCancellation?.Cancel();
        _runCancellation?.Dispose();
        _runCancellation = null;
        return Task.CompletedTask;
    }

    public async Task<SmartBpAutoRecognitionTickResult> RunOneTickAsync(BitmapSource frame, CancellationToken cancellationToken = default)
    {
        if (!await _tickGate.WaitAsync(0, cancellationToken))
            return Failure("An automatic recognition tick is already running.");
        string raw = "";
        try
        {
            var sequence = Interlocked.Increment(ref _frameSequence);
            frameRingBuffer.AddFrame(sequence, frame, DateTimeOffset.Now);
            var guidanceSnapshot = guidance.GetRuntimeSnapshot();
            var request = planner.BuildRequest(guidanceSnapshot, stateStore.Snapshot, ledger.GetSnapshot());
            SmartBpRegionSnapshot? regionSnapshot = null;
            SmartBpPhaseRecognitionResult phaseResult;
            SmartBpCroppedFrame? phaseCrop;
            IReadOnlyList<SmartBpCroppedFrame> contentCrops;
            var messages = new List<string>(request.Diagnostics);
            if (settings.Settings.UseMultiImageSnapshotRequest)
            {
                try
                {
                    var deltaPackage = await deltaRecognition.RecognizeDeltaAsync(frame, request, sequence, cancellationToken);
                    raw = string.Join("\n\n", deltaPackage.RawResponses.Select(item => $"{item.Key} raw:\n{item.Value}"));
                    messages.AddRange(deltaPackage.Diagnostics);
                    messages.AddRange(stateStore.ApplyDelta(deltaPackage.Delta, sequence, DateTimeOffset.Now));
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
                    regionSnapshot = await snapshotRecognition.RecognizeSnapshotAsync(frame, SmartBpRegionSnapshotRecognitionMode.PendingAndCurrentRegions, cancellationToken);
                    raw = string.Join("\n\n", regionSnapshot.RawResponses.Select(item => $"{item.Key} raw:\n{item.Value}"));
                    messages.AddRange(regionSnapshot.Diagnostics);
                    var fallbackDelta = ToDelta(regionSnapshot.BusinessState, request.RequestedFields);
                    messages.AddRange(stateStore.ApplyDelta(fallbackDelta, sequence, DateTimeOffset.Now));
                    phaseResult = regionSnapshot.Phase;
                    phaseCrop = regionSnapshot.PhaseCrop;
                    contentCrops = regionSnapshot.ContentCrops;
                }
            }
            else
            {
                messages.Add("Multi-image snapshot request is disabled; using sequential region fallback.");
                regionSnapshot = await snapshotRecognition.RecognizeSnapshotAsync(frame, SmartBpRegionSnapshotRecognitionMode.PendingAndCurrentRegions, cancellationToken);
                raw = string.Join("\n\n", regionSnapshot.RawResponses.Select(item => $"{item.Key} raw:\n{item.Value}"));
                messages.AddRange(regionSnapshot.Diagnostics);
                var fallbackDelta = ToDelta(regionSnapshot.BusinessState, request.RequestedFields);
                messages.AddRange(stateStore.ApplyDelta(fallbackDelta, sequence, DateTimeOffset.Now));
                phaseResult = regionSnapshot.Phase;
                phaseCrop = regionSnapshot.PhaseCrop;
                contentCrops = regionSnapshot.ContentCrops;
            }

            var state = stateStore.Snapshot;
            guidanceSnapshot = guidance.GetRuntimeSnapshot();
            var plan = backfill.BuildPlan(state, guidanceSnapshot);
            var operations = plan.StepCandidates.SelectMany(item => item.Operations).ToArray();
            messages.AddRange(plan.Diagnostics);
            messages.AddRange(plan.StepCandidates.Select(item => $"Step {item.StepIndex} {item.Action} [{string.Join(",", item.Indexes)}]: {item.Reason} Candidates={item.Operations.Count}."));
            var fingerprint = JsonSerializer.Serialize(state);
            _stableSnapshotCount = string.Equals(_lastSnapshotFingerprint, fingerprint, StringComparison.Ordinal)
                ? _stableSnapshotCount + 1
                : 1;
            _lastSnapshotFingerprint = fingerprint;
            var requiredStable = Math.Max(1, settings.Settings.RequiredStableSnapshots);
            SmartBpOperationApplyResult applyResult = settings.Settings.EnableAutoApplyRecognition && _stableSnapshotCount >= requiredStable
                ? await applier.ApplyAsync(operations, cancellationToken)
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
                    await Task.Delay(hold, cancellationToken);
                }
            }
            SmartBpGuidanceSyncResult? sync = settings.Settings.EnableAutoGuidanceSync
                ? await guidanceSync.SyncAsync(state, cancellationToken)
                : new(false, false, "Automatic GameGuidance synchronization is disabled.", null, [], null);
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
                operations, messages, applyResult, raw, null, snapshotForUi, plan, contentCrops);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (ex is LlamaCppRequestException request) raw = request.RawResponse;
            return new(null, null, null, null, null, null, guidance.GetRuntimeSnapshot(), [], [], null, raw, ex.Message);
        }
        finally { _tickGate.Release(); }
    }

    private SmartBpAutoRecognitionTickResult Failure(string error) =>
        new(null, null, null, null, null, null, guidance.GetRuntimeSnapshot(), [], [], null, "", error);

    public async Task<SmartBpStepCommitResult> ProcessTickAsync(BitmapSource frame, CancellationToken cancellationToken = default)
    {
        var result = await RunOneTickAsync(frame, cancellationToken).ConfigureAwait(false);
        if (result.BusinessState == null || result.BackfillPlan == null)
            throw new InvalidOperationException(result.Error ?? "SmartBP step commit tick failed.");
        return new(result.BusinessState, result.BackfillPlan, result.ApplyResult, result.GuidanceSync, result.CandidateMessages);
    }

    private static SmartBpSnapshotDeltaResult ToDelta(SmartBpBusinessStateRecognitionResult state, IReadOnlyCollection<string> requestedFields)
    {
        var updates = new List<SmartBpSnapshotFieldUpdate>();
        if (requestedFields.Contains("banned_sur"))
            updates.Add(new() { Field = "banned_sur", Slots = state.BannedSur.Select(ToPlayerSlot).ToList() });
        if (requestedFields.Contains("banned_hun"))
            updates.Add(new() { Field = "banned_hun", Slots = state.BannedHun.Select(ToPlayerSlot).ToList() });
        if (requestedFields.Contains("picked_sur"))
            updates.Add(new() { Field = "picked_sur", Slots = state.PickedSur.Select(ClonePlayerSlot).ToList() });
        if (requestedFields.Contains("picked_hun"))
            updates.Add(new() { Field = "picked_hun", PickedHun = ClonePlayerSlot(state.PickedHun) });
        return new() { Phase = state.Phase, Updates = updates };
    }

    private static SmartBpRecognizedPlayerCharacterSlot ToPlayerSlot(SmartBpRecognizedCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName };

    private static SmartBpRecognizedPlayerCharacterSlot ClonePlayerSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName, PlayerId = slot.PlayerId };
}
