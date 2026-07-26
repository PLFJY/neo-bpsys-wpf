using System.Collections.Generic;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

/// <summary>
/// v3 前台控件属性的保留字段集合，用于禁止存储访问器覆盖根级保留字段。
/// </summary>
/// <remarks>
/// 这些字段由宿主根布局 Host（Phase 2）或 <see cref="FrontedControlConfigBase"/> 基类管理，
/// 控件属性 Schema 不得通过任何存储访问器覆盖它们。
/// </remarks>
public static class FrontedV3ReservedFields
{
    private static readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "Left",
        "Top",
        "Width",
        "Height",
        "ZIndex",
        "Visibility",
        "BehaviorGuid",
        "GaussianBlur",
        "ControlType"
    };

    /// <summary>
    /// 返回给定字段名是否为保留字段。
    /// </summary>
    /// <param name="fieldName">要检查的字段名。</param>
    /// <returns>当字段为根级保留字段时为 <see langword="true"/>。</returns>
    public static bool IsReserved(string fieldName)
    {
        return !string.IsNullOrEmpty(fieldName) && _reserved.Contains(fieldName);
    }
}
