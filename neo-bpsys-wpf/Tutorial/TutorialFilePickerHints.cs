namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// 为教程引导的动作提供一次性文件选择器提示。
/// </summary>
public static class TutorialFilePickerHints
{
    private static string? _nextJsonInitialDirectory;
    private static string? _nextJsonTitle;

    /// <summary>
    /// 设置下一次 JSON 文件选择器的初始目录和标题。
    /// </summary>
    /// <param name="initialDirectory">下一次 JSON 选择器的初始目录。</param>
    /// <param name="title">可选的选择器标题。</param>
    public static void SetNextJsonPickerHint(string initialDirectory, string? title = null)
    {
        _nextJsonInitialDirectory = initialDirectory;
        _nextJsonTitle = title;
    }

    /// <summary>
    /// 消费下一次 JSON 选择器提示。
    /// </summary>
    /// <returns>下一次 JSON 选择器提示。</returns>
    public static TutorialJsonFilePickerHint ConsumeNextJsonPickerHint()
    {
        var hint = new TutorialJsonFilePickerHint(_nextJsonInitialDirectory, _nextJsonTitle);
        _nextJsonInitialDirectory = null;
        _nextJsonTitle = null;
        return hint;
    }
}

/// <summary>
/// 描述一次性教程 JSON 文件选择器提示。
/// </summary>
/// <param name="InitialDirectory">JSON 选择器的初始目录。</param>
/// <param name="Title">可选的选择器标题。</param>
public sealed record TutorialJsonFilePickerHint(string? InitialDirectory, string? Title);
