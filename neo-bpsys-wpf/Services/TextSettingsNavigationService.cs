using System.Windows.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Services;

public class TextSettingsNavigationService : ITextSettingsNavigationService
{
    private readonly Dictionary<FrontWindowType, Frame> _textSettingsFrames = [];

    public void SetFrameControl(FrontWindowType windowType, Frame frame)
    {
        _textSettingsFrames.TryAdd(windowType, frame);
    }

    public void Navigate(FrontWindowType windowType, object page)
    {
        if (!_textSettingsFrames.TryGetValue(windowType, out var frame)) return;
        if (frame.CanGoBack)
            frame.RemoveBackEntry();
        frame.Navigate(page);
    }

    public void Close(FrontWindowType windowType)
    {
        if (!_textSettingsFrames.TryGetValue(windowType, out var frame)) return;
        frame.Navigate(null);
        if (frame.CanGoBack)
            frame.RemoveBackEntry();
    }
}