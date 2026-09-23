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
