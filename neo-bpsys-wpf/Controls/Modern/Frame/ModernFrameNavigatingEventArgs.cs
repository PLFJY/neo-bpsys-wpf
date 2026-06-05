#nullable enable

using System.Windows;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

public sealed class ModernFrameNavigatingEventArgs : ModernFrameNavigationEventArgs
{
    public ModernFrameNavigatingEventArgs(
        FrameworkElement content,
        object? parameter,
        ModernFrameNavigationMode navigationMode,
        ModernNavigationTransitionInfo? transitionInfo)
        : base(content, parameter, navigationMode, transitionInfo)
    {
    }

    public bool Cancel { get; set; }
}
