#nullable enable

using System;
using System.Windows;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

public class ModernFrameNavigationEventArgs : EventArgs
{
    public ModernFrameNavigationEventArgs(
        FrameworkElement content,
        object? parameter,
        ModernFrameNavigationMode navigationMode,
        ModernNavigationTransitionInfo? transitionInfo)
    {
        Content = content;
        Parameter = parameter;
        NavigationMode = navigationMode;
        TransitionInfo = transitionInfo;
    }

    public FrameworkElement Content { get; }

    public object? Parameter { get; }

    public ModernFrameNavigationMode NavigationMode { get; }

    public ModernNavigationTransitionInfo? TransitionInfo { get; }
}
