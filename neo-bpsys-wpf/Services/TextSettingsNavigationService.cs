using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Services;

public class TextSettingsNavigationService : ITextSettingsNavigationService
{
    private readonly Dictionary<FrontedWindowType, Frame> _textSettingsFrames = [];

    public void SetFrameControl(FrontedWindowType windowType, Frame frame)
    {
        _textSettingsFrames.TryAdd(windowType, frame);
    }

    public void Navigate(FrontedWindowType windowType, object page)
    {
        if (!_textSettingsFrames.TryGetValue(windowType, out var frame)) return;
        if (frame.CanGoBack)
            frame.RemoveBackEntry();
        frame.Navigate(page);
    }

    public void Close(FrontedWindowType windowType)
    {
        if (!_textSettingsFrames.TryGetValue(windowType, out var frame)) return;
        frame.Navigate(null);
        if (frame.CanGoBack)
            frame.RemoveBackEntry();
    }
}