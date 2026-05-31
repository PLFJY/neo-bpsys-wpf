using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Stores window-level Designer v3 layout options.
/// </summary>
public interface IFrontedWindowLayoutOptionsService
{
    FrontedWindowLayoutOptions LoadOptions(string windowTypeName);

    Task SaveOptionsAsync(
        string windowTypeName,
        FrontedWindowLayoutOptions options,
        CancellationToken cancellationToken = default);

    string GetUserOptionsPath(string windowTypeName);

    Task ResetOptionsAsync(string windowTypeName, CancellationToken cancellationToken = default);
}
