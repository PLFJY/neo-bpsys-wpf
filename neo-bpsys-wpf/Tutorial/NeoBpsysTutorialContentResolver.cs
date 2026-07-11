using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// 宿主侧 <see cref="ITutorialContentResolver"/> 实现，从 <c>TourContent.resx</c> 资源族解析步骤内容。
/// </summary>
public sealed class NeoBpsysTutorialContentResolver : ITutorialContentResolver
{
    /// <inheritdoc />
    public string Resolve(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return I18nHelper.GetLocalizedString(AppI18nDictionaries.TourContent, key);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ResolveLines(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return [];
        }

        var value = Resolve(key);
        if (string.IsNullOrEmpty(value) || value == key)
        {
            return [value];
        }

        return value.Split('\n', StringSplitOptions.None);
    }
}
