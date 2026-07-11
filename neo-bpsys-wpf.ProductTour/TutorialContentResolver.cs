namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Resolves localized step content (titles, descriptions, dialogue lines) from resource keys.
/// </summary>
/// <remarks>
/// The Product Tour library defines this abstraction so the host application can supply
/// a resolver backed by its own resource family (e.g. <c>TourContent.resx</c>).
/// The default implementation returns the key verbatim and is intended for tests and
/// design-time contexts where no host resource family is available.
/// </remarks>
public interface ITutorialContentResolver
{
    /// <summary>
    /// Resolves a single localized string from the specified resource key.
    /// </summary>
    /// <param name="key">Resource key. If null or empty, an empty string is returned.</param>
    /// <returns>The localized string, or the key itself when no translation is found.</returns>
    string Resolve(string? key);

    /// <summary>
    /// Resolves multiple dialogue lines from a single resource key.
    /// The value is split on newline characters.
    /// </summary>
    /// <param name="key">Resource key. If null or empty, an empty list is returned.</param>
    /// <returns>The resolved dialogue lines, or a list containing only the key when no translation is found.</returns>
    IReadOnlyList<string> ResolveLines(string? key);
}

/// <summary>
/// Default content resolver that returns keys verbatim without localization.
/// </summary>
public sealed class DefaultTutorialContentResolver : ITutorialContentResolver
{
    /// <inheritdoc />
    public string Resolve(string? key) =>
        string.IsNullOrWhiteSpace(key) ? string.Empty : key;

    /// <inheritdoc />
    public IReadOnlyList<string> ResolveLines(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return [];
        }

        return [key];
    }
}
