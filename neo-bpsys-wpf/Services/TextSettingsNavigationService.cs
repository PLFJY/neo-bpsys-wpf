using neo_bpsys_wpf.Abstractions.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Extensions;
using System.Diagnostics;
using neo_bpsys_wpf.Enums;

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