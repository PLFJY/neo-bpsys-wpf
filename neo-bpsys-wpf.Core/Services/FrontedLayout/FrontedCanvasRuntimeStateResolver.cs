using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 为 Designer v3 Canvas 选择运行时状态，不进行窗口特定的分支处理。
/// </summary>
public static class FrontedCanvasRuntimeStateResolver
{
    public const string Bo3StateKey = "Bo3";

    public static FrontedCanvasRuntimeState Resolve(
        FrontedCanvasConfig config,
        ISharedDataService sharedDataService,
        ILogger? logger = null)
    {
        if (!config.EnableBoModeStates || !sharedDataService.IsBo3Mode)
        {
            return CreateRootState(config, isFallback: false);
        }

        if (config.BoModeStates.TryGetValue(Bo3StateKey, out var bo3State))
        {
            return new FrontedCanvasRuntimeState
            {
                CanvasWidth = config.CanvasWidth,
                CanvasHeight = config.CanvasHeight,
                BackgroundImage = bo3State.BackgroundImage,
                RequiredPlugins = bo3State.RequiredPlugins,
                Controls = bo3State.Controls,
                IsFallback = false
            };
        }

        logger?.LogWarning(
            "Fronted canvas has BO mode states enabled but Bo3 state is missing. Falling back to root/BO5 state.");
        return CreateRootState(config, isFallback: true);
    }

    private static FrontedCanvasRuntimeState CreateRootState(FrontedCanvasConfig config, bool isFallback) =>
        new()
        {
            CanvasWidth = config.CanvasWidth,
            CanvasHeight = config.CanvasHeight,
            BackgroundImage = config.BackgroundImage,
            RequiredPlugins = config.RequiredPlugins,
            Controls = config.Controls,
            IsFallback = isFallback
        };
}
