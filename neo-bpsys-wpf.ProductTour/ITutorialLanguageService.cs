namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Describes one language option shown by tutorial onboarding UI.
/// </summary>
public sealed class TutorialLanguageOption
{
    /// <summary>Gets the stable language option id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the native language name, when available.</summary>
    public string? NativeName { get; init; }

    /// <summary>Gets a value indicating whether this option follows the system language.</summary>
    public bool IsSystemDefault { get; init; }

    /// <summary>Gets a value indicating whether this option is currently selected.</summary>
    public bool IsSelected { get; init; }
}

/// <summary>
/// Applies the language selected from a tutorial welcome overlay.
/// </summary>
public interface ITutorialLanguageService
{
    /// <summary>Gets language options that can be selected from the tutorial welcome overlay.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Available language options.</returns>
    Task<IReadOnlyList<TutorialLanguageOption>> GetLanguageOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies and persists the selected language.</summary>
    /// <param name="languageOptionId">Language option id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ApplyLanguageAsync(string languageOptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Occurs when the tutorial language has changed, either through
    /// <see cref="ApplyLanguageAsync"/> or from an external source (e.g. settings page).
    /// Overlays subscribe to this event to hot-refresh displayed text.
    /// </summary>
    event EventHandler? LanguageChanged;
}

/// <summary>
/// No-op tutorial language service.
/// </summary>
public sealed class NoOpTutorialLanguageService : ITutorialLanguageService
{
    /// <inheritdoc />
    public event EventHandler? LanguageChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TutorialLanguageOption>> GetLanguageOptionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TutorialLanguageOption>>(
        [
            new TutorialLanguageOption { Id = "System", DisplayName = "跟随系统", NativeName = "Follow system", IsSystemDefault = true, IsSelected = true },
            new TutorialLanguageOption { Id = "zh_Hans", DisplayName = "简体中文", NativeName = "简体中文" },
            new TutorialLanguageOption { Id = "en_US", DisplayName = "English", NativeName = "English" },
            new TutorialLanguageOption { Id = "ja_JP", DisplayName = "日本語", NativeName = "日本語" }
        ]);

    /// <inheritdoc />
    public Task ApplyLanguageAsync(string languageOptionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
