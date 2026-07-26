namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 描述教程新手引导 UI 中显示的一个语言选项。
/// </summary>
public sealed class TutorialLanguageOption
{
    /// <summary>获取稳定的语言选项 id。</summary>
    public required string Id { get; init; }

    /// <summary>获取显示名称。</summary>
    public required string DisplayName { get; init; }

    /// <summary>获取该语言的原生名称（如有）。</summary>
    public string? NativeName { get; init; }

    /// <summary>获取一个值，指示该选项是否跟随系统语言。</summary>
    public bool IsSystemDefault { get; init; }

    /// <summary>获取一个值，指示该选项当前是否被选中。</summary>
    public bool IsSelected { get; init; }
}

/// <summary>
/// 应用从教程欢迎遮罩中选择的语言。
/// </summary>
public interface ITutorialLanguageService
{
    /// <summary>获取可从教程欢迎遮罩中选择的语言选项。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可用的语言选项。</returns>
    Task<IReadOnlyList<TutorialLanguageOption>> GetLanguageOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>应用并持久化所选语言。</summary>
    /// <param name="languageOptionId">语言选项 id。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ApplyLanguageAsync(string languageOptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 当教程语言发生更改时发生，更改可能来自
    /// <see cref="ApplyLanguageAsync"/> 或外部来源（如设置页）。
    /// 遮罩订阅此事件以热刷新所显示的文本。
    /// </summary>
    event EventHandler? LanguageChanged;
}

/// <summary>
/// 空实现的教程语言服务。
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
