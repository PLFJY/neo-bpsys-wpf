using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 集中生成前台可见的地图名称文本。
/// </summary>
public static class MapNameDisplayHelper
{
    public static string Format(Map? map, string? emptyText = null)
    {
        if (map is null)
        {
            return emptyText ?? string.Empty;
        }

        return I18nHelper.GetLocalizedString(map.Value.ToString());
    }
}
