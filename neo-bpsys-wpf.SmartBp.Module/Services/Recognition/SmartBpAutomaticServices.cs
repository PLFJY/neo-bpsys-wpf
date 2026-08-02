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

/// <summary>
/// SmartBP 自动识别阶段、GameGuidance 动作和裁剪区域之间的映射表。
/// </summary>
internal static class SmartBpAutomaticMapping
{
    /// <summary>
    /// AI/OCR 识别结果允许返回的阶段名称集合。
    /// </summary>
    public static readonly IReadOnlyList<string> ValidPhases =
    [
        "屏蔽求生者", "屏蔽监管者", "选择求生者", "求生者选择角色中", "选择监管者",
        "求生者选择天赋中", "监管者选择天赋中", "天赋已锁定",
        "即将进入区域选择", "区域选择", "求生者选择区域中", "监管者选择区域中",
        "等待游戏开始", "加载中", "对局中", "等待中", "未知"
    ];

    /// <summary>
    /// 将 GameGuidance 动作映射为 prompt 中使用的区域、阵营和含义说明。
    /// </summary>
    /// <param name="action">GameGuidance 动作。</param>
    /// <returns>区域 ID、阵营和含义说明。</returns>
    /// <exception cref="NotSupportedException">动作不属于当前 BP 识别支持范围时抛出。</exception>
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

    /// <summary>
    /// 将 GameGuidance 动作转换为自动识别任务类型。
    /// </summary>
    /// <param name="action">GameGuidance 动作。</param>
    /// <returns>识别任务类型。</returns>
    /// <exception cref="NotSupportedException">动作不属于当前 BP 识别支持范围时抛出。</exception>
    public static SmartBpRecognitionTask ToRecognitionTask(GameAction action) => action switch
    {
        GameAction.BanSur => SmartBpRecognitionTask.BanSur,
        GameAction.BanHun => SmartBpRecognitionTask.BanHun,
        GameAction.PickSur => SmartBpRecognitionTask.PickSur,
            GameAction.DistributeChara => SmartBpRecognitionTask.CharacterDistribution,
            GameAction.PickHun => SmartBpRecognitionTask.PickHun,
        _ => throw new NotSupportedException($"GameGuidance action {action} is not supported by BP recognition.")
    };

    /// <summary>
    /// 将识别输出中的动作文本解析为 GameGuidance 动作。
    /// </summary>
    /// <param name="value">识别输出动作名。</param>
    /// <param name="action">解析得到的动作。</param>
    /// <returns>解析成功返回 <see langword="true"/>。</returns>
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

    /// <summary>
    /// 将阶段名称映射到最接近的 GameGuidance 动作。
    /// </summary>
    /// <param name="phase">识别阶段名。</param>
    /// <param name="action">映射得到的动作。</param>
    /// <returns>存在动作映射返回 <see langword="true"/>。</returns>
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

    /// <summary>
    /// 判断动作是否属于可自动应用角色变更的 BP 角色操作。
    /// </summary>
    /// <param name="action">GameGuidance 动作。</param>
    /// <returns>角色操作返回 <see langword="true"/>。</returns>
    public static bool IsCharacterOperationAction(GameAction action) =>
        action is GameAction.BanSur or GameAction.BanHun or GameAction.PickSur or GameAction.DistributeChara or GameAction.PickHun;

    /// <summary>
    /// 求生者选择锁定后不再按视觉槽位索引合并的权威阶段集合。
    /// 仅包含「分配角色」之后明确不再进行求生者角色选择的阶段。
    /// 注意：<c>求生者选择角色中</c> 不在此集合内，因为它在 PickSur 阶段仍可能按视觉槽位索引合并。
    /// </summary>
    public static readonly IReadOnlyCollection<string> SurvivorPickLockedPhases = new HashSet<string>(StringComparer.Ordinal)
    {
        "求生者选择天赋中",
        "选择监管者", "监管者选择天赋中", "天赋已锁定",
        "即将进入区域选择", "区域选择", "求生者选择区域中", "监管者选择区域中",
        "等待游戏开始", "加载中", "对局中"
    };

    /// <summary>
    /// 判断当前是否处于求生者选择锁定状态。
    /// 锁定后 picked_sur 视觉槽位更新不得按索引直接合并到内部状态，只能按 player_id 生成分配交换。
    /// 优先级：GameGuidance 已启动时信任 guidance action；未启动时保守使用权威阶段名。
    /// </summary>
    /// <param name="snapshot">当前 GameGuidance 运行时快照。</param>
    /// <param name="authoritativePhase">权威识别阶段名。</param>
    /// <returns>锁定返回 <see langword="true"/>。</returns>
    public static bool IsSurvivorPickLocked(GameGuidanceRuntimeSnapshot snapshot, string authoritativePhase)
    {
        // 优先级 1：GameGuidance 已启动时，信任 guidance action / workflow 位置。
        if (snapshot.IsStarted)
        {
            // PickSur 明确不锁定：即使权威阶段是「求生者选择角色中」也允许按槽位索引合并。
            if (snapshot.CurrentAction == GameAction.PickSur)
                return false;
            // DistributeChara 及之后动作锁定。
            if (snapshot.CurrentAction is GameAction.DistributeChara or GameAction.PickSurTalent or GameAction.PickHun or GameAction.PickHunTalent or GameAction.EndGuidance)
                return true;
            // 若工作流中已执行过 DistributeChara，则视为锁定（防止 action 回退）。
            if (snapshot.Workflow.Any(step => step.StepIndex < snapshot.CurrentStepIndex && step.Action == GameAction.DistributeChara))
                return true;
        }
        // 优先级 2：GameGuidance 未启动或 action 未知时，保守使用权威阶段名。
        return SurvivorPickLockedPhases.Contains(authoritativePhase);
    }

    /// <summary>
    /// 将 GameGuidance 动作转换为 SmartBP 阶段名。
    /// </summary>
    /// <param name="action">GameGuidance 动作。</param>
    /// <returns>阶段名。</returns>
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

    /// <summary>
    /// 获取某个角色操作需要重点刷新和解析的画面区域及字段名。
    /// </summary>
    /// <param name="action">GameGuidance 动作。</param>
    /// <returns>识别区域和业务字段名。</returns>
    /// <exception cref="NotSupportedException">动作没有角色字段目标时抛出。</exception>
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

/// <summary>
/// 解析并校验 OCR 返回的完整 BP 业务状态 JSON。
/// </summary>
internal static class SmartBpBusinessStateParser
{
    /// <summary>
    /// 解析完整业务状态 JSON。
    /// </summary>
    /// <param name="raw">OCR 原始 JSON 文本。</param>
    /// <returns>规范化后的业务状态。</returns>
    /// <exception cref="InvalidDataException">JSON 为空或字段不符合契约时抛出。</exception>
    public static SmartBpBusinessStateRecognitionResult Parse(string raw)
    {
        var result = JsonSerializer.Deserialize<SmartBpBusinessStateRecognitionResult>(raw)
            ?? throw new InvalidDataException("Business-state recognition JSON is empty.");
        NormalizeAndValidate(result);
        return result;
    }

    /// <summary>
    /// 规范化并校验完整业务状态对象。
    /// </summary>
    /// <param name="result">待校验的业务状态对象。</param>
    /// <exception cref="InvalidDataException">阶段、槽位数量或索引不符合契约时抛出。</exception>
    public static void NormalizeAndValidate(SmartBpBusinessStateRecognitionResult result)
    {
        if (!SmartBpAutomaticMapping.ValidPhases.Contains(result.Phase))
            throw new InvalidDataException("Invalid BP phase.");
        result.BannedSur ??= [];
        result.BannedHun ??= [];
        result.PickedSur ??= [];
        result.PickedHun ??= new();
        result.DistributionEvidence ??= [];
        ValidateCharacterSlots(result.BannedSur, 4, "banned_sur");
        ValidateCharacterSlots(result.BannedHun, 2, "banned_hun");
        ValidatePlayerSlots(result.PickedSur, 4, "picked_sur");
        if (result.PickedHun.Index != 0) throw new InvalidDataException("picked_hun.index must be 0.");
        Normalize(result.PickedHun);
    }

    /// <summary>
    /// 判断识别文本是否表示未选择槽位。
    /// </summary>
    /// <param name="value">角色名文本。</param>
    /// <returns>表示未选择返回 <see langword="true"/>。</returns>
    public static bool IsUnselected(string? value) => string.Equals(NormalizeName(value), "未选择", StringComparison.Ordinal);

    /// <summary>
    /// 校验 ban 位等仅包含角色名的槽位集合。
    /// </summary>
    /// <param name="slots">槽位集合。</param>
    /// <param name="count">期望槽位数量。</param>
    /// <param name="field">字段名。</param>
    private static void ValidateCharacterSlots(List<SmartBpRecognizedCharacterSlot> slots, int count, string field)
    {
        if (slots.Count != count) throw new InvalidDataException($"{field} must contain exactly {count} entries.");
        var expected = Enumerable.Range(0, count).ToArray();
        if (!slots.Select(x => x.Index).OrderBy(x => x).SequenceEqual(expected))
            throw new InvalidDataException($"{field} must contain indexes {string.Join(",", expected)}.");
        foreach (var slot in slots) Normalize(slot);
    }

    /// <summary>
    /// 校验 pick 位等同时包含角色和玩家 ID 的槽位集合。
    /// </summary>
    /// <param name="slots">槽位集合。</param>
    /// <param name="count">期望槽位数量。</param>
    /// <param name="field">字段名。</param>
    private static void ValidatePlayerSlots(List<SmartBpRecognizedPlayerCharacterSlot> slots, int count, string field)
    {
        if (slots.Count != count) throw new InvalidDataException($"{field} must contain exactly {count} entries.");
        var expected = Enumerable.Range(0, count).ToArray();
        if (!slots.Select(x => x.Index).OrderBy(x => x).SequenceEqual(expected))
            throw new InvalidDataException($"{field} must contain indexes {string.Join(",", expected)}.");
        foreach (var slot in slots) Normalize(slot);
    }

    /// <summary>
    /// 规范化角色槽位的角色名。
    /// </summary>
    /// <param name="slot">角色槽位。</param>
    private static void Normalize(SmartBpRecognizedCharacterSlot slot) => slot.CharacterName = NormalizeName(slot.CharacterName);

    /// <summary>
    /// 将空值、unknown 和 null 文本统一成“未选择”。
    /// </summary>
    /// <param name="value">原始角色名。</param>
    /// <returns>规范化角色名。</returns>
    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "未选择";
        var trimmed = value.Trim();
        return trimmed.Equals("unknown", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("null", StringComparison.OrdinalIgnoreCase)
            ? "未选择"
            : trimmed;
    }
}

/// <summary>
/// 将识别出的 BP 业务状态格式化为调试文本。
/// </summary>
internal static class SmartBpBusinessStateFormatter
{
    /// <summary>
    /// 格式化完整业务状态。
    /// </summary>
    /// <param name="value">业务状态。</param>
    /// <param name="resolver">角色解析器，用于可选输出 resolved 名称。</param>
    /// <param name="includeResolved">是否包含解析后的角色名。</param>
    /// <returns>调试文本。</returns>
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

    /// <summary>
    /// 追加仅包含角色名的槽位段落。
    /// </summary>
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

    /// <summary>
    /// 追加包含角色名和玩家 ID 的槽位段落。
    /// </summary>
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

/// <summary>
/// 解析 SmartBP 自动识别 OCR 输出的严格 JSON 结构。
/// </summary>
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

internal sealed class SmartBpCandidateOperationBuilder(
    ISmartBpCharacterResolver resolver,
    ISharedDataService shared,
    ISmartBpPlayerIdentityMatcher matcher)
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
            GameAction.DistributeChara => BuildDistribution(
                state.DistributionEvidence.Count > 0 ? state.DistributionEvidence : state.PickedSur,
                guidanceIndexes),
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
            if (guidanceIndexes.Count > 0 && !guidanceIndexes.Contains(slot.Index))
            {
                messages.Add($"Skipped: index not in current GameGuidance indexes ({camp}[{slot.Index}] {slot.CharacterName}).");
                continue;
            }
            if (slot.SlotState == SmartBpRecognizedSlotState.Unknown)
            {
                messages.Add($"Skipped: Unknown observation cannot modify host state ({camp}[{slot.Index}]).");
                continue;
            }
            if (slot.SlotState == SmartBpRecognizedSlotState.Empty)
            {
                var emptyConfidence = slot.IsAutoApplySafe ? slot.RecognitionConfidence : Math.Min(slot.RecognitionConfidence, .89);
                operations.Add(new(SmartBpDetectedOperationKind.CommitEmptyBan, action, guidanceIndexes.ToArray(), camp,
                    slot.Index, null, null, null, emptyConfidence,
                    slot.RecognitionReason ?? "Explicit empty Ban observation."));
                continue;
            }
            if (SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)) continue;
            var confidence = slot.IsAutoApplySafe ? slot.RecognitionConfidence : Math.Min(slot.RecognitionConfidence, .89);
            var resolved = resolver.Resolve(slot.CharacterName, camp, slot.Index, confidence);
            operations.Add(new(kind, action, guidanceIndexes.ToArray(), camp, slot.Index, slot.CharacterName,
                resolved.ResolvedCharacterName, null, confidence,
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
            var internalSlot = hunterSlot ? -1 : slot.Index;
            if (!hunterSlot && guidanceIndexes.Count > 0 && !guidanceIndexes.Contains(internalSlot))
            {
                messages.Add($"Skipped: index not in current GameGuidance indexes ({camp}[{internalSlot}] {slot.CharacterName}).");
                continue;
            }
            if (slot.SlotState == SmartBpRecognizedSlotState.Unknown)
            {
                messages.Add($"Skipped: Unknown observation cannot modify host state ({camp}[{internalSlot}]).");
                continue;
            }
            if (slot.SlotState == SmartBpRecognizedSlotState.Empty)
            {
                var emptyKind = hunterSlot
                    ? SmartBpDetectedOperationKind.CommitEmptyHunterPick
                    : SmartBpDetectedOperationKind.CommitEmptySurvivorPick;
                var emptyConfidence = slot.IsAutoApplySafe ? slot.RecognitionConfidence : Math.Min(slot.RecognitionConfidence, .89);
                operations.Add(new(emptyKind, action, guidanceIndexes.ToArray(), camp, internalSlot,
                    null, null, slot.PlayerId, emptyConfidence,
                    slot.RecognitionReason ?? "Explicit empty Pick observation."));
                continue;
            }
            if (SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)) continue;
            var confidence = slot.IsAutoApplySafe ? slot.RecognitionConfidence : Math.Min(slot.RecognitionConfidence, .89);
            var resolved = resolver.Resolve(slot.CharacterName, camp, internalSlot, confidence);
            operations.Add(new(kind, action, guidanceIndexes.ToArray(), camp, internalSlot, slot.CharacterName,
                resolved.ResolvedCharacterName, slot.PlayerId, confidence,
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
                slot.CharacterName, resolved.ResolvedCharacterName,
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
        var evidence = slots
            .Where(x => x.SlotState == SmartBpRecognizedSlotState.Selected)
            .Where(x => x.IsAutoApplySafe && x.RecognitionConfidence >= .95)
            .Where(x => !SmartBpBusinessStateParser.IsUnselected(x.CharacterName) && x.Index is >= 0 and < 4)
            .OrderBy(x => x.Index)
            .ToArray();
        var resolvedRoles = new List<(SmartBpRecognizedPlayerCharacterSlot Slot, SmartBpNormalizedCharacter Character, double Confidence)>();
        foreach (var slot in evidence)
        {
            var playerIdText = string.IsNullOrWhiteSpace(slot.PlayerId) ? "<missing>" : slot.PlayerId;
            messages.Add($"Distribution visual slot {slot.Index}: char={slot.CharacterName}, player_id={playerIdText}.");
            var confidence = slot.RecognitionConfidence;
            var resolved = resolver.Resolve(slot.CharacterName, Camp.Sur, slot.Index, confidence);
            if (resolved.ResolvedCharacterName == null)
            {
                messages.Add($"Skipped distribution visual slot {slot.Index}: unresolved character '{slot.CharacterName}'.");
                continue;
            }
            resolvedRoles.Add((slot, resolved, confidence));
        }

        var duplicateRoles = resolvedRoles
            .GroupBy(item => item.Character.ResolvedCharacterName!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (duplicateRoles.Count > 0)
            messages.Add($"Distribution recovery rejected duplicated role evidence=[{string.Join(',', duplicateRoles)}]; duplicated roles are not filled or shifted.");
        var uniqueRoles = resolvedRoles
            .Where(item => !duplicateRoles.Contains(item.Character.ResolvedCharacterName!))
            .ToArray();

        var simulated = shared.CurrentGame.SurPlayerList.Select(x => x.Character?.Name).ToArray();
        var missingRoles = uniqueRoles
            .Where(item => Array.FindIndex(simulated, name => string.Equals(
                name,
                item.Character.ResolvedCharacterName,
                StringComparison.Ordinal)) < 0)
            .OrderBy(item => item.Slot.Index)
            .ToArray();
        var recoveryGroup = missingRoles.Length > 0 ? $"distribution-recovery:{Guid.NewGuid():N}" : null;
        foreach (var item in missingRoles)
        {
            var emptySlot = Array.FindIndex(simulated, string.IsNullOrWhiteSpace);
            if (emptySlot < 0)
            {
                messages.Add($"Distribution recovery held {item.Character.ResolvedCharacterName}: no empty survivor slot remains; existing selections are preserved.");
                break;
            }
            operations.Add(new(SmartBpDetectedOperationKind.PickSurvivor, GameAction.DistributeChara,
                guidanceIndexes.ToArray(), Camp.Sur, emptySlot, item.Slot.CharacterName,
                item.Character.ResolvedCharacterName, item.Slot.PlayerId,
                item.Confidence, $"Distribution recovery: safe current-frame role evidence fills available survivor slot {emptySlot}; visual slot {item.Slot.Index} is not treated as the internal Pick slot.",
                DependencyGroup: recoveryGroup, RequireEmptySurvivorSlot: true));
            simulated[emptySlot] = item.Character.ResolvedCharacterName;
            messages.Add($"Distribution recovery planned: fill available Sur[{emptySlot}] with {item.Character.ResolvedCharacterName} from visual slot {item.Slot.Index}.");
        }

        var assignments = new List<(SmartBpRecognizedPlayerCharacterSlot Slot, int Target, SmartBpNormalizedCharacter Character, string DisplayName, double Confidence)>();
        foreach (var item in uniqueRoles)
        {
            if (string.IsNullOrWhiteSpace(item.Slot.PlayerId))
            {
                messages.Add($"Distribution assignment skipped for visual slot {item.Slot.Index}: player_id missing; role recovery remains allowed.");
                continue;
            }
            var playerMatch = matcher.MatchSurvivorPlayer(item.Slot.PlayerId);
            if (!playerMatch.IsMatched || !playerMatch.IsSafe)
            {
                messages.Add($"Distribution assignment skipped for visual slot {item.Slot.Index}: player_id '{item.Slot.PlayerId}' did not match safely ({playerMatch.Reason}).");
                continue;
            }
            assignments.Add((item.Slot, playerMatch.Index, item.Character,
                playerMatch.DisplayName ?? item.Slot.PlayerId, item.Confidence));
            messages.Add($"Player identity matched: {item.Slot.PlayerId} -> internal Sur[{playerMatch.Index}] ({playerMatch.DisplayName}), score={playerMatch.Score:0.00}.");
        }

        var conflictedTargets = assignments
            .GroupBy(item => item.Target)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        foreach (var item in assignments.Where(item => !conflictedTargets.Contains(item.Target)))
        {
            var slot = item.Slot;
            var target = item.Target;
            var resolved = item.Character;

            var source = Array.FindIndex(simulated, x => x == resolved.ResolvedCharacterName);
            if (source < 0)
            {
                messages.Add($"Skipped distribution for player {item.DisplayName}: character {resolved.ResolvedCharacterName} is not among currently selected survivors; distribution cannot introduce new characters.");
                continue;
            }
            if (source == target)
            {
                messages.Add($"Skipped distribution no-op: player {item.DisplayName} already has {resolved.ResolvedCharacterName}.");
                continue;
            }

            messages.Add($"Distribution operation: swap existing {resolved.ResolvedCharacterName} source={source} target={target}.");
            operations.Add(new(SmartBpDetectedOperationKind.SwapSurvivors, GameAction.DistributeChara,
                guidanceIndexes.ToArray(), Camp.Sur, target, slot.CharacterName,
                resolved.ResolvedCharacterName, slot.PlayerId,
                item.Confidence, $"Distribution: place existing character {resolved.ResolvedCharacterName} onto player {item.DisplayName} internal slot {target}.",
                DependencyGroup: recoveryGroup));
            (simulated[source], simulated[target]) = (simulated[target], simulated[source]);
        }
        return new(operations, messages);
    }

    private IReadOnlyList<SmartBpDetectedOperation> BuildDistribution(
        SmartBpFocusedExtractionResult extraction,
        IReadOnlyList<int> guidanceIndexes)
    {
        var slots = extraction.Slots
            .Where(x => x.CharacterName != null && !SmartBpBusinessStateParser.IsUnselected(x.CharacterName) && x.SlotIndex is >= 0 and < 4)
            .Select(x => new SmartBpRecognizedPlayerCharacterSlot
            {
                Index = x.SlotIndex,
                CharacterName = x.CharacterName!,
                PlayerId = x.PlayerId,
                SlotState = SmartBpRecognizedSlotState.Selected,
                RecognitionConfidence = x.Confidence,
                IsAutoApplySafe = x.Confidence >= .90
            });
        return BuildDistribution(slots, guidanceIndexes).Operations;
    }
}

internal sealed class SmartBpDetectedOperationApplier(
    ICharacterSelectionService selection,
    IGameGuidanceService guidance,
    ISharedDataService shared,
    ISmartBpRecognitionSettingsService settings) : ISmartBpDetectedOperationApplier
{
    public async Task<SmartBpOperationApplyResult> ApplyAsync(IReadOnlyList<SmartBpDetectedOperation> operations, CancellationToken cancellationToken = default)
    {
        var messages = new List<string>();
        var applied = 0;
        var skipped = 0;
        var failedDependencyGroups = new HashSet<string>(StringComparer.Ordinal);
        if (operations.Count == 0)
            return new(0, 0, ["No candidate operations to apply."]);
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.DependencyGroup is { } dependencyGroup && failedDependencyGroups.Contains(dependencyGroup))
            {
                skipped++;
                messages.Add($"Skipped: dependency group {dependencyGroup} was stopped by an earlier unsafe operation for {Describe(operation)}.");
                continue;
            }
            var snapshot = guidance.GetRuntimeSnapshot();
            if (!ValidateWorkflowSource(operation, snapshot, out var workflowError)) { skipped++; MarkDependencyFailed(operation, failedDependencyGroups); messages.Add($"Skipped: {workflowError} for {Describe(operation)}."); continue; }
            if (operation.Confidence < 0.90) { skipped++; MarkDependencyFailed(operation, failedDependencyGroups); messages.Add($"Skipped: low confidence for {Describe(operation)}."); continue; }
            var isEmptyCommit = operation.Kind is SmartBpDetectedOperationKind.CommitEmptyBan or
                SmartBpDetectedOperationKind.CommitEmptySurvivorPick or
                SmartBpDetectedOperationKind.CommitEmptyHunterPick;
            Character? character = null;
            if (!isEmptyCommit)
            {
                if (operation.ResolvedCharacterName == null) { skipped++; MarkDependencyFailed(operation, failedDependencyGroups); messages.Add($"Skipped: unresolved character for {Describe(operation)}."); continue; }
                var dictionary = operation.Camp == Camp.Sur ? shared.SurCharaDict : shared.HunCharaDict;
                if (!dictionary.TryGetValue(operation.ResolvedCharacterName, out character)) { skipped++; MarkDependencyFailed(operation, failedDependencyGroups); messages.Add($"Skipped: resolved character name no longer exists: {operation.ResolvedCharacterName}."); continue; }
            }
            var playAnimation = operation.ApplyMode != SmartBpDetectedOperationApplyMode.FreeSync;
            if (playAnimation && settings.Settings.RecognitionVisualBufferMilliseconds > 0)
                await Task.Delay(settings.Settings.RecognitionVisualBufferMilliseconds, cancellationToken);

            switch (operation.Kind)
            {
                case SmartBpDetectedOperationKind.BanCharacter:
                    if (!TryGetBanSlot(operation.Camp, operation.SlotIndex, out var banned))
                    {
                        skipped++;
                        messages.Add($"Skipped: invalid ban slot for {Describe(operation)}.");
                        continue;
                    }
                    var banCommitState = operation.Camp == Camp.Sur
                        ? selection.GetCurrentBpSlotCommitState().SurvivorBans[operation.SlotIndex]
                        : selection.GetCurrentBpSlotCommitState().HunterBans[operation.SlotIndex];
                    if (IsSameCharacter(banned, character) && banCommitState == BpSlotCommitState.CommittedCharacter)
                    {
                        skipped++;
                        messages.Add($"Skipped: no-op same ban for {Describe(operation)}.");
                        continue;
                    }
                    await selection.BanCharacterAsync(operation.Camp, operation.SlotIndex, character, playAnimation);
                    messages.Add($"{AppliedPrefix(operation)} BanCharacter {operation.Camp}[{operation.SlotIndex}] {character!.Name}");
                    break;
                case SmartBpDetectedOperationKind.CommitEmptyBan:
                    var banStates = operation.Camp == Camp.Sur
                        ? selection.GetCurrentBpSlotCommitState().SurvivorBans
                        : selection.GetCurrentBpSlotCommitState().HunterBans;
                    if (operation.SlotIndex < 0 || operation.SlotIndex >= banStates.Count)
                    {
                        skipped++;
                        continue;
                    }
                    if (banStates[operation.SlotIndex] == BpSlotCommitState.CommittedEmpty)
                    {
                        skipped++;
                        messages.Add($"Skipped: host already contains explicit empty Ban for {Describe(operation)}.");
                        continue;
                    }
                    await selection.CommitEmptyBanAsync(operation.Camp, operation.SlotIndex, playAnimation);
                    messages.Add($"Applied explicit empty Ban {operation.Camp}[{operation.SlotIndex}].");
                    break;
                case SmartBpDetectedOperationKind.PickSurvivor:
                    if (operation.SlotIndex is < 0 or >= 4)
                    {
                        skipped++;
                        MarkDependencyFailed(operation, failedDependencyGroups);
                        messages.Add($"Skipped: invalid survivor slot for {Describe(operation)}.");
                        continue;
                    }
                    if (operation.RequireEmptySurvivorSlot && shared.CurrentGame.SurPlayerList[operation.SlotIndex].Character != null)
                    {
                        skipped++;
                        MarkDependencyFailed(operation, failedDependencyGroups);
                        messages.Add($"Skipped: survivor recovery target is no longer empty for {Describe(operation)}.");
                        continue;
                    }
                    if (IsSameCharacter(shared.CurrentGame.SurPlayerList[operation.SlotIndex].Character, character) &&
                        selection.GetCurrentBpSlotCommitState().SurvivorPicks[operation.SlotIndex] == BpSlotCommitState.CommittedCharacter)
                    {
                        skipped++;
                        messages.Add($"Skipped: no-op same character for {Describe(operation)}.");
                        continue;
                    }
                    await selection.SelectSurvivorAsync(operation.SlotIndex, character, playAnimation);
                    messages.Add($"{AppliedPrefix(operation)} PickSurvivor Sur[{operation.SlotIndex}] {character!.Name}");
                    break;
                case SmartBpDetectedOperationKind.PickHunter:
                    if (IsSameCharacter(shared.CurrentGame.HunPlayer.Character, character) &&
                        selection.GetCurrentBpSlotCommitState().HunterPick == BpSlotCommitState.CommittedCharacter)
                    {
                        skipped++;
                        messages.Add($"Skipped: no-op same character for {Describe(operation)}.");
                        continue;
                    }
                    await selection.SelectHunterAsync(character, playAnimation);
                    messages.Add($"{AppliedPrefix(operation)} PickHunter {character!.Name}");
                    break;
                case SmartBpDetectedOperationKind.CommitEmptySurvivorPick:
                    if (operation.SlotIndex is < 0 or >= 4)
                    {
                        skipped++;
                        continue;
                    }
                    await selection.CommitEmptySurvivorPickAsync(operation.SlotIndex, playAnimation);
                    messages.Add($"Applied explicit empty survivor Pick Sur[{operation.SlotIndex}].");
                    break;
                case SmartBpDetectedOperationKind.CommitEmptyHunterPick:
                    await selection.CommitEmptyHunterPickAsync(playAnimation);
                    messages.Add("Applied explicit empty hunter Pick.");
                    break;
                case SmartBpDetectedOperationKind.SwapSurvivors:
                    if (operation.SlotIndex is < 0 or >= 4)
                    {
                        skipped++;
                        MarkDependencyFailed(operation, failedDependencyGroups);
                        messages.Add($"Skipped: invalid survivor swap target for {Describe(operation)}.");
                        continue;
                    }
                    if (IsSameCharacter(shared.CurrentGame.SurPlayerList[operation.SlotIndex].Character, character))
                    {
                        skipped++;
                        messages.Add($"Skipped: no-op same character for {Describe(operation)}.");
                        continue;
                    }
                    var sourceMatch = shared.CurrentGame.SurPlayerList
                        .Select((player, index) => (player, index))
                        .FirstOrDefault(x => IsSameCharacter(x.player.Character, character));
                    if (sourceMatch.player == null) { skipped++; MarkDependencyFailed(operation, failedDependencyGroups); messages.Add($"Skipped: no source slot contains target character for {Describe(operation)}."); continue; }
                    var source = sourceMatch.index;
                    if (source == operation.SlotIndex)
                    {
                        skipped++;
                        messages.Add($"Skipped: no-op swap source and target are the same for {Describe(operation)}.");
                        continue;
                    }
                    await selection.SwapSurvivorsAsync(source, operation.SlotIndex, playAnimation);
                    messages.Add($"{AppliedPrefix(operation)} SwapSurvivors source={source} target={operation.SlotIndex} {character!.Name}");
                    break;
            }
            applied++;
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
                SmartBpDetectedOperationKind.CommitEmptyBan => operation.Camp is Camp.Sur or Camp.Hun && operation.SlotIndex >= 0,
                SmartBpDetectedOperationKind.PickSurvivor => operation.Camp == Camp.Sur && operation.SlotIndex is >= 0 and < 4,
                SmartBpDetectedOperationKind.CommitEmptySurvivorPick => operation.Camp == Camp.Sur && operation.SlotIndex is >= 0 and < 4,
                SmartBpDetectedOperationKind.PickHunter => operation.Camp == Camp.Hun && operation.SlotIndex == -1,
                SmartBpDetectedOperationKind.CommitEmptyHunterPick => operation.Camp == Camp.Hun && operation.SlotIndex == -1,
                _ => false
            };
            error = valid ? "" : "invalid free-sync operation contract";
            return valid;
        }
        if (operation.ApplyMode == SmartBpDetectedOperationApplyMode.CurrentStep)
        {
            if (snapshot.CurrentAction is not { } currentAction)
            {
                error = "current GameGuidance action is unavailable";
                return false;
            }
            var currentPosition = new SmartBpWorkflowPosition(currentAction, snapshot.CurrentIndexes);
            var sourcePosition = new SmartBpWorkflowPosition(
                operation.SourceGuidanceAction,
                operation.SourceGuidanceIndexes);
            if (!currentPosition.Equals(sourcePosition))
            {
                error = $"GameGuidance position changed from {sourcePosition} to {currentPosition}";
                return false;
            }
            if (operation.SourceWorkflowStepIndex is { } sourceStep && snapshot.CurrentStepIndex != sourceStep)
            {
                error = $"GameGuidance step changed from {sourceStep} to {snapshot.CurrentStepIndex}";
                return false;
            }
            error = "";
            return true;
        }

        if (operation.ApplyMode == SmartBpDetectedOperationApplyMode.AutomaticSupplement)
        {
            if (operation.SourceWorkflowStepIndex is not { } sourceStepIndex)
            {
                error = "automatic supplement source step is unavailable";
                return false;
            }
            var sourceStep = snapshot.Workflow.FirstOrDefault(step => step.StepIndex == sourceStepIndex);
            if (sourceStep is null ||
                !new SmartBpWorkflowPosition(sourceStep.Action, sourceStep.Indexes).Equals(
                    new SmartBpWorkflowPosition(operation.SourceGuidanceAction, operation.SourceGuidanceIndexes)))
            {
                error = $"automatic supplement source position does not match workflow step {sourceStepIndex}";
                return false;
            }
            if (sourceStepIndex >= snapshot.CurrentStepIndex)
            {
                error = $"automatic supplement source step {sourceStepIndex} is not earlier than current step {snapshot.CurrentStepIndex}";
                return false;
            }
            var validKind = operation.Kind is SmartBpDetectedOperationKind.BanCharacter or
                SmartBpDetectedOperationKind.PickSurvivor or
                SmartBpDetectedOperationKind.PickHunter;
            error = validKind ? "" : "automatic supplement only accepts concrete character operations";
            return validKind;
        }

        error = "unsupported operation apply mode";
        return false;
    }

    private static string AppliedPrefix(SmartBpDetectedOperation operation) => operation.ApplyMode switch
    {
        SmartBpDetectedOperationApplyMode.CurrentStep => "Guided catch-up with animation: Applied",
        SmartBpDetectedOperationApplyMode.AutomaticSupplement => "Automatic same-slot supplement with animation: Applied",
        _ => "Force-synced without animation: Applied"
    };

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
        return !string.IsNullOrWhiteSpace(left.Name) &&
               !string.IsNullOrWhiteSpace(right.Name) &&
               string.Equals(left.Name, right.Name, StringComparison.Ordinal);
    }

    private static string Describe(SmartBpDetectedOperation operation) =>
        $"{operation.Kind} {operation.Camp}[{operation.SlotIndex}] {operation.RawCharacterName ?? "null"}";

    private static void MarkDependencyFailed(SmartBpDetectedOperation operation, ISet<string> failedDependencyGroups)
    {
        if (!string.IsNullOrWhiteSpace(operation.DependencyGroup))
            failedDependencyGroups.Add(operation.DependencyGroup);
    }
}

internal enum SmartBpRecognitionDebugMode
{
    Automatic,
    FullStrategy,
    CurrentStageIncremental
}

internal sealed class SmartBpAutoRecognitionCoordinator(
    ISmartBpSnapshotRecognitionPlanner planner,
    ISmartBpFrameRingBuffer frameRingBuffer,
    ISmartBpRecognitionSettingsService settings,
    IGameGuidanceService guidance,
    ICharacterSelectionService selection,
    SmartBpCandidateOperationBuilder candidateBuilder,
    ISmartBpSceneGateService sceneGate,
    ISmartBpOcrBpRecognitionService ocrRecognition,
    SmartBpHistoricalFrameReviewService historicalReview,
    ISmartBpReconciliationService reconciliation) : ISmartBpAutoRecognitionCoordinator
{
    private readonly SemaphoreSlim _tickGate = new(1, 1);
    private readonly object _cancellationLock = new();
    private CancellationTokenSource? _runCancellation;
    private CancellationTokenSource? _currentTickCancellation;
    private string? _lastSnapshotFingerprint;
    private int _stableSnapshotCount;
    private long _frameSequence;
    private bool _hasDetectedPostBp;
    private string _postBpPhase = "未知";
    private long _postBpDetectedFrameSequence;
    private int _transitionToAreaSelectionConsecutiveCount;
    private SmartBpLifecycleCategory _lastStableLifecycleCategory = SmartBpLifecycleCategory.Unknown;
    public bool IsRunning => _runCancellation is { IsCancellationRequested: false };

    /// <inheritdoc />
    public void SampleFrame(BitmapSource frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var sequence = Interlocked.Increment(ref _frameSequence);
        frameRingBuffer.AddFrame(sequence, frame, DateTimeOffset.Now);
    }

    /// <inheritdoc />
    public void ResetCaptureContext()
    {
        CancelCurrentAutomaticTick();
        frameRingBuffer.Reset();
        _lastSnapshotFingerprint = null;
        _stableSnapshotCount = 0;
    }

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

    // OCR-only 执行矩阵：完整调试 OCR 所有 BP 字段；仅阶段识别只 OCR 状态/阶段；自动模式使用规划器请求的字段。
    public async Task<SmartBpAutoRecognitionTickResult> RunFullRecognitionDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RecognizeFullBpSnapshotAsync(frame, isDryRun: false, cancellationToken).ConfigureAwait(false);

    public async Task<SmartBpAutoRecognitionTickResult> RecognizeFullBpSnapshotAsync(
        BitmapSource frame,
        bool isDryRun,
        CancellationToken cancellationToken = default)
    {
        CancelCurrentAutomaticTick();
        return await RunOneTickCoreAsync(
            frame,
            isDryRun,
            cancellationToken,
            SmartBpRecognitionDebugMode.FullStrategy,
            linkToAutomaticRunCancellation: false,
            waitForRunningTick: true).ConfigureAwait(false);
    }

    public async Task<SmartBpAutoRecognitionTickResult> RunIncrementalRecognitionDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RunOneTickCoreAsync(frame, isDryRun: false, cancellationToken, SmartBpRecognitionDebugMode.CurrentStageIncremental).ConfigureAwait(false);

    public async Task<SmartBpAutoRecognitionTickResult> RunPhaseOnlyDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RunPhaseOnlyDebugCoreAsync(frame, cancellationToken).ConfigureAwait(false);

    private async Task<SmartBpAutoRecognitionTickResult> RunOneTickCoreAsync(
        BitmapSource frame,
        bool isDryRun,
        CancellationToken cancellationToken = default,
        SmartBpRecognitionDebugMode debugMode = SmartBpRecognitionDebugMode.Automatic,
        bool linkToAutomaticRunCancellation = true,
        bool waitForRunningTick = false)
    {
        var gateAcquired = waitForRunningTick
            ? await _tickGate.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false)
            : await _tickGate.WaitAsync(0, cancellationToken).ConfigureAwait(false);
        if (!gateAcquired)
            return Failure("An automatic recognition tick is already running.");
        var tickStartedAt = Stopwatch.GetTimestamp();
        CancellationTokenSource linked;
        lock (_cancellationLock)
        {
            linked = linkToAutomaticRunCancellation
                ? CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _runCancellation?.Token ?? CancellationToken.None)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (linkToAutomaticRunCancellation)
                _currentTickCancellation = linked;
        }
        var tickToken = linked.Token;
        var isDebugPreview = debugMode != SmartBpRecognitionDebugMode.Automatic;
        string raw = "";
        try
        {
            var sequence = Interlocked.Increment(ref _frameSequence);
            var captureTimestamp = DateTimeOffset.Now;
            if (!isDebugPreview && !isDryRun && _hasDetectedPostBp)
                return CreateLatchedPostBpResult(sequence);
            frameRingBuffer.AddFrame(sequence, frame, captureTimestamp);
            var guidanceSnapshot = guidance.GetRuntimeSnapshot();
            var request = debugMode == SmartBpRecognitionDebugMode.FullStrategy
                ? BuildFullStrategyDebugRequest(CreateCurrentFrameState("未知"))
                : planner.BuildRequest(guidanceSnapshot);
            SmartBpRegionSnapshot? regionSnapshot = null;
            SmartBpPhaseRecognitionResult phaseResult;
            SmartBpCroppedFrame? phaseCrop;
            IReadOnlyList<SmartBpCroppedFrame> contentCrops;
            SmartBpBusinessStateRecognitionResult state = CreateCurrentFrameState("未知");
            var messages = new List<string>(request.Diagnostics);
            if (isDryRun)
                messages.Add("Speed-test dry run: recognition request shape matches automatic tick, but local merge, auto apply, and GameGuidance sync are disabled.");
            if (debugMode == SmartBpRecognitionDebugMode.FullStrategy)
                messages.Add("Full strategy debug: phase_top plus all four BP business fields are requested.");
            else if (debugMode == SmartBpRecognitionDebugMode.CurrentStageIncremental)
                messages.Add("Current-stage incremental debug: automatic planner requested only relevant/stale fields; operation apply and guidance sync are disabled.");
            IReadOnlyDictionary<string, string> rawResponses;
            var recognitionPath = ResolveRecognitionPath(request);
            messages.Add($"Recognition path: {recognitionPath}; requested_fields=[{string.Join(", ", request.RequestedFields)}].");
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
                    phaseResult = new SmartBpPhaseRecognitionResult { Phase = localStatus.Phase.Phase };
                    state = CreateCurrentFrameState(phaseResult.Phase);
                    var statusGate = sceneGate.Classify(phaseResult, state,
                        new Dictionary<string, string> { ["top_left_status"] = statusRaw }, guidanceSnapshot);
                    messages.Add($"TopLeftStatus hard confirmation: {phaseResult.Phase}; final_phase={phaseResult.Phase}.");
                    return LatchAndCreatePostBpPausedResult(
                        sequence, state, phaseResult, null, guidanceSnapshot, messages, statusRaw, statusGate);
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
                            state = CreateCurrentFrameState(phaseResult.Phase);
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
            if (!isDebugPreview)
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
                state = CreateCurrentFrameState(phaseResult.Phase);
                rawResponses = new Dictionary<string, string> { ["ocr_phase"] = phaseRaw };
                messages.AddRange(phaseOnlyOcr.Diagnostics);
                var preContentGate = sceneGate.Classify(phaseResult, state, rawResponses, guidanceSnapshot);
                messages.Add(preContentGate.ShouldPauseAutomaticRecognition
                    ? $"Post-BP phase detected: phase={phaseResult.Phase}; scene={preContentGate.Scene}."
                    : $"OCR phase gate: phase={phaseResult.Phase}; scene={preContentGate.Scene}.");
                if (preContentGate.ShouldPauseAutomaticRecognition)
                    return LatchAndCreatePostBpPausedResult(sequence,
                        state, phaseResult, phaseCrop, guidanceSnapshot, messages, phaseRaw, preContentGate);
                request = FilterAutomaticRequestByPhase(request, phaseResult.Phase, messages);
                messages.Add("OCR phase gate allowed BP content recognition.");
            }
            else
            {
                var phaseOnlyOcr = await ocrRecognition.RecognizeAsync(
                    frame,
                    new SmartBpOcrRecognitionRequest([], IncludePhase: true),
                    tickToken).ConfigureAwait(false);
                phaseResult = phaseOnlyOcr.Phase;
                phaseCrop = null;
                raw = string.Join(Environment.NewLine, phaseOnlyOcr.Regions
                    .SelectMany(region => region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text}")));
                rawResponses = new Dictionary<string, string> { ["ocr_phase"] = raw };
                messages.AddRange(phaseOnlyOcr.Diagnostics);
            }

            var gateBeforeContent = sceneGate.Classify(phaseResult, state, rawResponses, guidanceSnapshot);
            if ((!gateBeforeContent.IsBpRecognitionAllowed && !isDebugPreview) || request.RequestedRegions.Count == 0)
            {
                contentCrops = [];
                messages.Add(request.RequestedRegions.Count == 0
                    ? "OCR skipped content recognition because no fields were requested."
                    : $"OCR skipped content recognition because BP recognition is blocked by the scene decision: {gateBeforeContent.Reason}.");
            }
            else
            {
                var tickMode = DescribeRecognitionTickMode(debugMode);
                messages.Add($"OCR role-distribution diagnostics: saved_frame_id={sequence}; tick_mode={tickMode}; planner_requested_fields=[{string.Join(", ", request.RequestedFields)}]; ocr_requested_regions=[{string.Join(", ", request.RequestedRegions.Select(item => $"{item.Region}->{item.TargetField}"))}].");
                var ocrParseContext = new SmartBpOcrFieldParseContext
                {
                    AuthoritativePhase = phaseResult.Phase,
                    CurrentGuidanceAction = debugMode == SmartBpRecognitionDebugMode.FullStrategy
                        ? null
                        : guidanceSnapshot.CurrentAction,
                    SurvivorPickLocked = debugMode != SmartBpRecognitionDebugMode.FullStrategy &&
                                         SmartBpAutomaticMapping.IsSurvivorPickLocked(guidanceSnapshot, phaseResult.Phase),
                    IsAutomaticMode = !isDebugPreview,
                    IsGlobalSnapshot = debugMode == SmartBpRecognitionDebugMode.FullStrategy
                };
                var ocr = await ocrRecognition.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest(
                    request.RequestedRegions.Select(item => item.Region).Distinct().ToArray(),
                    IncludePhase: false,
                    ParseContext: ocrParseContext), tickToken);
                var ocrRaw = string.Join(Environment.NewLine, ocr.Regions.SelectMany(region =>
                    region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text} conf={line.Confidence:0.00}")));
                raw = string.IsNullOrWhiteSpace(raw) ? ocrRaw : raw + "\n\nocr raw:\n" + ocrRaw;
                rawResponses = new Dictionary<string, string>(rawResponses) { ["ocr"] = ocrRaw };
                messages.Add($"OCR role-distribution diagnostics: phase_result={phaseResult.Phase}; ocr_raw_lines=[{string.Join(" | ", ocr.Regions.SelectMany(region => region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text}")))}].");
                messages.Add($"OCR current-frame state={FormatBusinessStateForDiagnostics(ocr.BusinessState)}.");
                messages.AddRange(ocr.Diagnostics);
                if (!string.Equals(ocr.BusinessState.Phase, phaseResult.Phase, StringComparison.Ordinal))
                {
                    messages.Add($"OCR content phase={ocr.BusinessState.Phase} ignored; authoritative phase gate remains {phaseResult.Phase}.");
                    ocr.BusinessState.Phase = phaseResult.Phase;
                }
                state = ocr.BusinessState;
                messages.AddRange(ApplyCurrentFrameGuards(state, phaseResult.Phase, guidanceSnapshot));
                messages.Add($"OCR guarded current-frame state={FormatBusinessStateForDiagnostics(state)}.");
                contentCrops = [];
            }

            guidanceSnapshot = guidance.GetRuntimeSnapshot();
            var gate = sceneGate.Classify(phaseResult, state, rawResponses, guidanceSnapshot);
            messages.Add($"Scene: {gate.Scene}; BP recognition allowed: {gate.IsBpRecognitionAllowed}; Character operations allowed: {gate.IsCharacterOperationAllowed}; Action: {(gate.ShouldPauseAutomaticRecognition ? "automatic recognition paused" : "continue monitoring")}; Reason: {gate.Reason}.");
            if (!isDebugPreview && !isDryRun && gate.ShouldPauseAutomaticRecognition)
                return LatchAndCreatePostBpPausedResult(sequence, state, phaseResult, phaseCrop,
                    guidanceSnapshot, messages, raw, gate);
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
                    [], messages, dryRunApply, raw, null, dryRunSnapshot, contentCrops, gate);
            }

            SmartBpCatchUpTriggerDecision? catchUpTrigger = null;
            var requiresGuidanceStart = !guidanceSnapshot.IsStarted &&
                                        settings.Settings.EnableAutoGuidanceSync &&
                                        SmartBpAutomaticMapping.TryMapPhase(state.Phase, out _);
            if (!isDebugPreview && gate.IsCharacterOperationAllowed &&
                (settings.Settings.EnableAutoApplyRecognition || settings.Settings.EnableAutoGuidanceSync))
            {
                catchUpTrigger = SmartBpCatchUpTriggerEvaluator.Evaluate(
                    guidanceSnapshot,
                    state,
                    selection.GetCurrentBpSlotCommitState());
                messages.Add($"Automatic catch-up precheck: {catchUpTrigger.Reason}.");
                if (catchUpTrigger.ShouldReviewHistory)
                {
                    var review = await historicalReview.SupplementAsync(
                        state,
                        sequence,
                        guidanceSnapshot,
                        tickToken).ConfigureAwait(false);
                    state = review.State;
                    messages.AddRange(review.Diagnostics);
                    messages.Add($"Historical review summary: reviewed_frames={review.ReviewedFrameCount}; supplemented_slots={review.SupplementedSlotCount}; merge_mode=supplement-only.");
                    catchUpTrigger = SmartBpCatchUpTriggerEvaluator.Evaluate(
                        guidanceSnapshot,
                        state,
                        selection.GetCurrentBpSlotCommitState());
                }
                else
                {
                    messages.Add("Historical review not triggered; no historical OCR was scheduled.");
                }
            }

            var operations = gate.IsCharacterOperationAllowed
                ? BuildCurrentFrameOperations(state, candidateBuilder)
                : [];
            var distributionOperations = gate.IsCharacterOperationAllowed &&
                                         catchUpTrigger?.TargetStep?.Action == GameAction.DistributeChara
                ? candidateBuilder.BuildWithDiagnostics(
                    state,
                    GameAction.DistributeChara,
                    catchUpTrigger.TargetStep.Indexes).Operations.ToArray()
                : [];
            if (distributionOperations.Length > 0)
                operations = operations.Concat(distributionOperations).ToArray();
            var fingerprint = JsonSerializer.Serialize(state);
            _stableSnapshotCount = string.Equals(_lastSnapshotFingerprint, fingerprint, StringComparison.Ordinal)
                ? _stableSnapshotCount + 1
                : 1;
            _lastSnapshotFingerprint = fingerprint;
            var requiredStable = Math.Max(1, settings.Settings.RequiredStableSnapshots);
            var shouldRunReconciliation = requiresGuidanceStart ||
                                          catchUpTrigger?.ShouldReconcile == true ||
                                          distributionOperations.Length > 0;
            SmartBpReconciliationResult? reconciliationResult = isDebugPreview
                ? null
                : gate.IsCharacterOperationAllowed &&
                  (settings.Settings.EnableAutoApplyRecognition || settings.Settings.EnableAutoGuidanceSync) &&
                  shouldRunReconciliation &&
                  _stableSnapshotCount >= requiredStable
                    ? await reconciliation.ReconcileAsync(state, SmartBpReconciliationMode.Automatic, tickToken)
                    : null;
            if (reconciliationResult is not null)
                messages.AddRange(reconciliationResult.Diagnostics);
            SmartBpOperationApplyResult applyResult = isDebugPreview
                ? new(0, operations.Length, ["Recognition debug preview: operation application is disabled."])
                : reconciliationResult is not null
                    ? new(
                        reconciliationResult.CharacterApplyResult.AppliedCount + reconciliationResult.EmptyApplyResult.AppliedCount,
                        reconciliationResult.CharacterApplyResult.SkippedCount + reconciliationResult.EmptyApplyResult.SkippedCount,
                        reconciliationResult.CharacterApplyResult.Messages.Concat(reconciliationResult.EmptyApplyResult.Messages).ToArray())
                : !shouldRunReconciliation
                    ? new(0, operations.Length, ["Skipped: automatic catch-up trigger was not met; Action/Indexes are aligned and no Pending/CommittedEmpty slot has new role evidence."])
                : settings.Settings.EnableAutoApplyRecognition
                    ? new(0, operations.Length, [$"Skipped: waiting for stable BP observations ({_stableSnapshotCount}/{requiredStable})."])
                    : new(0, operations.Length, operations.Length == 0
                    ? ["Skipped: auto apply disabled; no candidate operations were generated."]
                    : operations.Select(x => $"Skipped: auto apply disabled for step {x.SourceWorkflowStepIndex} {x.Kind} {x.Camp}[{x.SlotIndex}] {x.RawCharacterName ?? "null"}.").ToArray());
            SmartBpGuidanceSyncResult? sync = reconciliationResult is not null && settings.Settings.EnableAutoGuidanceSync
                ? new(
                    reconciliationResult.GuidanceResult.Moved,
                    reconciliationResult.GuidanceResult.Succeeded,
                    reconciliationResult.GuidanceResult.Message,
                    reconciliationResult.GuidanceResult.TargetAction,
                    reconciliationResult.GuidanceResult.TargetIndexes,
                    reconciliationResult.GuidanceResult.TargetStepIndex)
                : new(false, !isDebugPreview && settings.Settings.EnableAutoGuidanceSync && !shouldRunReconciliation,
                    isDebugPreview
                        ? "Recognition debug preview: GameGuidance synchronization is disabled."
                        : settings.Settings.EnableAutoGuidanceSync && !shouldRunReconciliation
                            ? "Automatic catch-up was not triggered because Action/Indexes are aligned and no Pending/CommittedEmpty slot has new role evidence."
                            : "Automatic GameGuidance synchronization is disabled.", null, [], null);
            var finalGuidanceSnapshot = guidance.GetRuntimeSnapshot();
            var progressSync = reconciliationResult?.GuidanceResult;
            if (progressSync?.Moved == true)
                finalGuidanceSnapshot = guidance.GetRuntimeSnapshot();
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
                operations, messages, applyResult, raw, null, snapshotForUi, contentCrops, gate, progressSync);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(null, null, null, null, null, null, guidance.GetRuntimeSnapshot(), [], [], null, raw, ex.Message);
        }
        finally
        {
            frameRingBuffer.ReportOcrProcessingDuration(Stopwatch.GetElapsedTime(tickStartedAt));
            lock (_cancellationLock)
            {
                if (linkToAutomaticRunCancellation && ReferenceEquals(_currentTickCancellation, linked))
                    _currentTickCancellation = null;
            }
            linked.Dispose();
            _tickGate.Release();
        }
    }

    private void CancelCurrentAutomaticTick()
    {
        lock (_cancellationLock)
        {
            _currentTickCancellation?.Cancel();
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
        var state = CreateCurrentFrameState(_postBpPhase);
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
        var state = CreateCurrentFrameState("未知");
        var phase = new SmartBpPhaseRecognitionResult { Phase = state.Phase };
        var gate = new SmartBpSceneGateResult(SmartBpRecognitionScene.Unknown, false, false, false, reason);
        messages.Add("content_recognition_allowed=False; no BP OCR, Business OCR fusion, field merge, or candidate operation generation was run.");
        return new(state, phase, null, null, null, null, guidance.GetRuntimeSnapshot(), [], messages.ToArray(),
            new SmartBpOperationApplyResult(0, 0, []), raw, null, null, [], gate);
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
            "屏蔽求生者" or "屏蔽监管者" or "选择求生者" or "求生者选择角色中" or
                "求生者选择天赋中" or "选择监管者" or "监管者选择天赋中" =>
                new HashSet<string>(["banned_sur", "banned_hun", "picked_sur", "picked_hun"], StringComparer.Ordinal),
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
            SmartBpLifecycleCategory.SurvivorTalentAdjust or SmartBpLifecycleCategory.HunterTalentAdjust =>
                new(["banned_sur", "banned_hun", "picked_sur", "picked_hun"], StringComparer.Ordinal),
            _ => new(StringComparer.Ordinal)
        };
        if (allowedFields == null) return request;
        var filtered = request.RequestedRegions.Where(item => allowedFields.Contains(item.TargetField)).ToArray();
        var removed = request.RequestedFields.Where(field => !allowedFields.Contains(field)).ToArray();
        if (removed.Length > 0)
            diagnostics?.Add($"Lifecycle-aware field filter removed [{string.Join(", ", removed)}] because category={category}.");
        return new SmartBpSnapshotDeltaRequest(filtered, request.Diagnostics, request.CurrentKnownState);
    }

    private static IReadOnlyList<string> ApplyCurrentFrameGuards(
        SmartBpBusinessStateRecognitionResult state,
        string authoritativePhase,
        GameGuidanceRuntimeSnapshot guidanceSnapshot)
    {
        state.Phase = authoritativePhase;
        return
        [
            $"Current-frame slot evidence retained without SmartBP state merge; guidanceStep={guidanceSnapshot.CurrentStepIndex}; action={guidanceSnapshot.CurrentAction}."
        ];
    }

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
            raw, null, null, [], gate);
    }

    private async Task<SmartBpAutoRecognitionTickResult> RunPhaseOnlyDebugCoreAsync(
        BitmapSource frame,
        CancellationToken cancellationToken = default)
    {
        if (!await _tickGate.WaitAsync(0, cancellationToken))
            return Failure("An automatic recognition tick is already running.");
        try
        {
            SmartBpPhaseRecognitionResult phaseResult;
            SmartBpCroppedFrame? phaseCrop;
            string raw;
            IReadOnlyDictionary<string, string> rawResponses;
            var messages = new List<string>
            {
                "Phase-only debug: strategy=PureOcr; no field OCR, merge, operations, or apply."
            };

            var ocr = await ocrRecognition.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest([], IncludePhase: true), cancellationToken).ConfigureAwait(false);
            phaseResult = ocr.Phase;
            phaseCrop = null;
            raw = string.Join(Environment.NewLine, ocr.Regions.SelectMany(region => region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text}")));
            rawResponses = new Dictionary<string, string> { ["ocr_phase"] = raw };
            messages.AddRange(ocr.Diagnostics);

            var state = CreateCurrentFrameState(phaseResult.Phase);
            var gate = sceneGate.Classify(phaseResult, state, rawResponses, guidance.GetRuntimeSnapshot());
            messages.Add($"Scene: {gate.Scene}; BP recognition allowed: {gate.IsBpRecognitionAllowed}; Character operations allowed: {gate.IsCharacterOperationAllowed}; Action: {(gate.ShouldPauseAutomaticRecognition ? "automatic recognition paused" : "continue monitoring")}; Reason: {gate.Reason}.");
            return new(state, phaseResult, null, phaseCrop, null, null, guidance.GetRuntimeSnapshot(),
                [], messages, new(0, 0, ["Phase-only debug: operation generation is disabled."]), raw, null, null, [], gate);
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
    /// 解析协调器在当前 tick 中应使用的识别路径。
    /// 没有请求字段时使用仅阶段路径；请求一个或多个字段时使用字段快照路径。
    /// </summary>
    /// <param name="request">规划器构建的识别请求。</param>
    /// <returns>识别路径枚举值。</returns>
    private static SmartBpRecognitionPath ResolveRecognitionPath(SmartBpSnapshotDeltaRequest request) =>
        request.RequestedFields.Count == 0 ? SmartBpRecognitionPath.PhaseOnly : SmartBpRecognitionPath.FieldSnapshot;

    private static SmartBpDetectedOperation[] BuildCurrentFrameOperations(
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

    private static string DescribeRecognitionTickMode(SmartBpRecognitionDebugMode debugMode) =>
        debugMode switch
        {
            SmartBpRecognitionDebugMode.FullStrategy => "full image debug",
            SmartBpRecognitionDebugMode.CurrentStageIncremental => "incremental debug",
            _ => "automatic"
        };

    private static string FormatBusinessStateForDiagnostics(SmartBpBusinessStateRecognitionResult state) =>
        JsonSerializer.Serialize(CreateCurrentKnownStateJson(state));

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

    private static SmartBpBusinessStateRecognitionResult CreateCurrentFrameState(string phase) =>
        new()
        {
            Phase = phase,
            BannedSur = Enumerable.Range(0, 4).Select(index => new SmartBpRecognizedCharacterSlot { Index = index }).ToList(),
            BannedHun = Enumerable.Range(0, 2).Select(index => new SmartBpRecognizedCharacterSlot { Index = index }).ToList(),
            PickedSur = Enumerable.Range(0, 4).Select(index => new SmartBpRecognizedPlayerCharacterSlot { Index = index }).ToList(),
            PickedHun = new SmartBpRecognizedPlayerCharacterSlot { Index = 0 }
        };
}
