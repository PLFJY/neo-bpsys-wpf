using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 旧版以画布为中心的迁移代码的内部转换帮助程序。
/// </summary>
public static class FrontedWindowConfigCanvasAdapter
{
    /// <summary>
    /// 从旧版以画布为中心的配置创建以窗口为中心的配置。
    /// </summary>
    /// <param name="canvasConfig">旧版以画布为中心的配置。</param>
    /// <returns>包含相同画布和控件数据的以窗口为中心的配置。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="canvasConfig"/> 为 <see langword="null"/> 时抛出。</exception>
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
    /// 将以窗口为中心的配置转换为旧版以画布为中心的模型。
    /// </summary>
    /// <param name="windowConfig">以窗口为中心的配置。</param>
    /// <returns>包含相同画布和控件数据的旧版以画布为中心的配置。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="windowConfig"/> 为 <see langword="null"/> 时抛出。</exception>
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
