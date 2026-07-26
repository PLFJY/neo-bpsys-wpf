#nullable enable

using System;
using System.Windows;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

internal sealed class ModernFrameJournalEntry
{
    private readonly FrameworkElement? _content;
    private readonly Func<FrameworkElement>? _contentFactory;

    public ModernFrameJournalEntry(FrameworkElement content, object? parameter, ModernNavigationTransitionInfo? transitionInfo)
    {
        _content = content;
        Parameter = parameter;
        TransitionInfo = transitionInfo;
    }

    public ModernFrameJournalEntry(Func<FrameworkElement> contentFactory, object? parameter, ModernNavigationTransitionInfo? transitionInfo)
    {
        _contentFactory = contentFactory;
        Parameter = parameter;
        TransitionInfo = transitionInfo;
    }

    public object? Parameter { get; }

    public ModernNavigationTransitionInfo? TransitionInfo { get; }

    public FrameworkElement CreateContent()
    {
        return _content ?? _contentFactory?.Invoke() ?? throw new InvalidOperationException("Journal entry does not contain content.");
    }
}
