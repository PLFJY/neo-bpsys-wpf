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
