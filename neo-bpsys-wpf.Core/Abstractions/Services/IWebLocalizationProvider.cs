namespace neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;

/// <summary>为 Web Renderer 提供主程序当前文化下的本地化快照。</summary>
public interface IWebLocalizationProvider
{
    /// <summary>根据布局引用的键生成快照。</summary>
    WebLocalizationSnapshot Create(IReadOnlyCollection<string> keys);
}

/// <summary>主程序本地化字典的只读投影。</summary>
public sealed record WebLocalizationSnapshot(string Culture, long Revision,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Dictionaries,
    IReadOnlyDictionary<string, string> AnyHost);

/// <summary>提供由 WPF helper 计算的对局进度语义。</summary>
public interface IWebGameProgressProvider
{
    /// <summary>生成指定进度和赛制的本地化显示数据。</summary>
    WebGameProgressSemanticState Create(GameProgress progress, bool isBo3Mode);
}

/// <summary>主程序 GameProgressDisplayHelper 的不可变投影。</summary>
public sealed record WebGameProgressSemanticState(int Ordinal, string Name, bool IsFree, int GameNumber,
    bool IsOvertime, string? Half, string GameText, string HalfText, string FullText);
