using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Abstractions.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Services
{
    public class TextSettingsNavigationService : ITextSettingsNavigationService
    {
        private readonly ILogger<TextSettingsNavigationService> _logger;
        private readonly Dictionary<FrontWindowType, Frame> _textSettingsFrames = [];

        public TextSettingsNavigationService(ILogger<TextSettingsNavigationService> logger)
        {
            _logger = logger;
            _logger.LogInformation("TextSettingsNavigationService initialized");
        }

        public void SetFrameControl(FrontWindowType windowType, Frame frame)
        {
            if (frame == null)
            {
                _logger.LogWarning("Attempted to set null frame for window type: {WindowType}", windowType);
                return;
            }

            if (_textSettingsFrames.TryAdd(windowType, frame))
            {
                _logger.LogInformation("Frame control set for window type: {WindowType}", windowType);
            }
            else
            {
                _logger.LogWarning("Frame control already set for window type: {WindowType}", windowType);
            }
        }

        public void Navigate(FrontWindowType windowType, object page)
        {
            _logger.LogInformation("Navigating to page for window type: {WindowType}, Page: {PageType}",
                windowType, page?.GetType().Name ?? "null");

            if (!_textSettingsFrames.TryGetValue(windowType, out var frame))
            {
                _logger.LogWarning("No frame registered for window type: {WindowType}", windowType);
                return;
            }

            if (page == null)
            {
                _logger.LogError("Attempted to navigate to null page for window type: {WindowType}", windowType);
                return;
            }

            try
            {
                if (frame.CanGoBack)
                {
                    _logger.LogDebug("Clearing back history before navigation");
                    frame.RemoveBackEntry();
                }

                frame.Navigate(page);
                _logger.LogInformation("Navigation completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation error for window type: {WindowType}, Page: {PageType}",
                    windowType, page.GetType().Name);
            }
        }

        public void Close(FrontWindowType windowType)
        {
            _logger.LogInformation("Closing navigation for window type: {WindowType}", windowType);

            if (!_textSettingsFrames.TryGetValue(windowType, out var frame))
            {
                _logger.LogWarning("No frame registered for window type: {WindowType}", windowType);
                return;
            }

            try
            {
                frame.Navigate(null);
                _logger.LogDebug("Cleared frame content");

                if (frame.CanGoBack)
                {
                    _logger.LogDebug("Clearing back history");
                    frame.RemoveBackEntry();
                }

                _logger.LogInformation("Close operation completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing navigation for window type: {WindowType}", windowType);
            }
        }
    }
}