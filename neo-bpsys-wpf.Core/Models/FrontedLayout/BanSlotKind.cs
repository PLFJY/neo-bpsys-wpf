using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 Ban 位来源。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BanSlotKind
{
    /// <summary>
    /// 当前局 Ban 位。
    /// </summary>
    Current,

    /// <summary>
    /// 全局 Ban 位。
    /// </summary>
    Global
}
