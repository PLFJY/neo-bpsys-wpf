using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>将主程序 GameProgressDisplayHelper 结果投影给 Web Renderer。</summary>
public sealed class WebGameProgressProvider : IWebGameProgressProvider
{
    /// <inheritdoc />
    public WebGameProgressSemanticState Create(GameProgress progress, bool isBo3Mode)
    {
        var parts = GameProgressDisplayHelper.GetParts(progress, isBo3Mode);
        return new WebGameProgressSemanticState((int)progress, progress.ToString(), parts.IsFree,
            parts.GameNumber ?? 0, parts.IsOvertime, parts.Half?.ToString(), parts.GameText, parts.HalfText, parts.FullText);
    }
}
