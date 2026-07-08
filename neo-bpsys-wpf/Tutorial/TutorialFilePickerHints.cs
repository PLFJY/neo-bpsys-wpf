namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Provides one-shot file picker hints for tutorial-guided actions.
/// </summary>
public static class TutorialFilePickerHints
{
    private static string? _nextJsonInitialDirectory;
    private static string? _nextJsonTitle;

    /// <summary>
    /// Sets the initial directory and title for the next JSON file picker.
    /// </summary>
    /// <param name="initialDirectory">Initial directory for the next JSON picker.</param>
    /// <param name="title">Optional picker title.</param>
    public static void SetNextJsonPickerHint(string initialDirectory, string? title = null)
    {
        _nextJsonInitialDirectory = initialDirectory;
        _nextJsonTitle = title;
    }

    /// <summary>
    /// Consumes the next JSON picker hint.
    /// </summary>
    /// <returns>The next JSON picker hint.</returns>
    public static TutorialJsonFilePickerHint ConsumeNextJsonPickerHint()
    {
        var hint = new TutorialJsonFilePickerHint(_nextJsonInitialDirectory, _nextJsonTitle);
        _nextJsonInitialDirectory = null;
        _nextJsonTitle = null;
        return hint;
    }
}

/// <summary>
/// Describes a one-shot tutorial JSON file picker hint.
/// </summary>
/// <param name="InitialDirectory">Initial directory for the JSON picker.</param>
/// <param name="Title">Optional picker title.</param>
public sealed record TutorialJsonFilePickerHint(string? InitialDirectory, string? Title);
