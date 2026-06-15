namespace neo_bpsys_wpf.Messages;

/// <summary>
/// Requests the fronted management page to switch its local tab.
/// </summary>
/// <param name="TabKey">Target local tab key.</param>
public sealed record FrontManageTabNavigationMessage(string TabKey)
{
    /// <summary>
    /// Key for the layout package management tab.
    /// </summary>
    public const string LayoutPackagesTabKey = "LayoutPackages";
}
