using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Internal conversion helpers for legacy canvas-centric migration code.
/// </summary>
public static class FrontedWindowConfigCanvasAdapter
{
    /// <summary>
    /// Creates a window-centric config from a legacy canvas-centric config.
    /// </summary>
    /// <param name="canvasConfig">Legacy canvas-centric config.</param>
    /// <returns>Window-centric config containing the same canvas and control data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvasConfig"/> is <see langword="null"/>.</exception>
    public static FrontedWindowConfig FromCanvasConfig(FrontedCanvasConfig canvasConfig)
    {
        ArgumentNullException.ThrowIfNull(canvasConfig);

        return new FrontedWindowConfig
        {
            Version = 3,
            WindowSettings = new FrontedWindowSettings
            {
                WindowWidth = canvasConfig.CanvasWidth,
                WindowHeight = canvasConfig.CanvasHeight
            },
            CanvasSettings = new FrontedCanvasSettings
            {
                CanvasWidth = canvasConfig.CanvasWidth,
                CanvasHeight = canvasConfig.CanvasHeight,
                BackgroundImage = canvasConfig.BackgroundImage,
                EnableBoModeStates = canvasConfig.EnableBoModeStates,
                BoModeStates = canvasConfig.BoModeStates
            },
            ControlLayout = new FrontedControlLayout
            {
                RequiredPlugins = canvasConfig.RequiredPlugins,
                Controls = canvasConfig.Controls
            }
        };
    }

    /// <summary>
    /// Converts a window-centric config to the legacy canvas-centric model.
    /// </summary>
    /// <param name="windowConfig">Window-centric config.</param>
    /// <returns>Legacy canvas-centric config containing the same canvas and control data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="windowConfig"/> is <see langword="null"/>.</exception>
    public static FrontedCanvasConfig ToCanvasConfig(FrontedWindowConfig windowConfig)
    {
        ArgumentNullException.ThrowIfNull(windowConfig);

        return new FrontedCanvasConfig
        {
            Version = windowConfig.Version,
            CanvasWidth = windowConfig.CanvasSettings.CanvasWidth,
            CanvasHeight = windowConfig.CanvasSettings.CanvasHeight,
            BackgroundImage = windowConfig.CanvasSettings.BackgroundImage,
            EnableBoModeStates = windowConfig.CanvasSettings.EnableBoModeStates,
            BoModeStates = windowConfig.CanvasSettings.BoModeStates,
            RequiredPlugins = windowConfig.ControlLayout.RequiredPlugins,
            Controls = windowConfig.ControlLayout.Controls
        };
    }
}
