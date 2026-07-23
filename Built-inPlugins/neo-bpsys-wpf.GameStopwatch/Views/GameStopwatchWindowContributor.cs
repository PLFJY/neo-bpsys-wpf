using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.GameStopwatch.ViewModels;

namespace neo_bpsys_wpf.GameStopwatch.Views;

/// <summary>注册比赛秒表插件前台窗口。</summary>
public sealed class GameStopwatchWindowContributor : IFrontedWindowPluginContributor
{
    /// <summary>比赛秒表窗口的稳定 ID。</summary>
    public const string WindowId = "A6B4CB0B-354B-4B66-8AB8-2E94C3F0E5D2";

    /// <inheritdoc />
    public IEnumerable<FrontedPluginWindowDescriptor> GetFrontedWindows()
    {
        yield return new FrontedPluginWindowDescriptor
        {
            PackageId = "neo_bpsys_wpf.GameStopwatch",
            WindowId = WindowId,
            WindowTypeName = "GameStopwatchWindow",
            DisplayName = "比赛秒表",
            Description = "用于直播画面的比赛秒表。",
            Kind = FrontedWindowKind.PluginXaml,
            WindowType = typeof(GameStopwatchWindow),
            ViewModelType = typeof(GameStopwatchWindowViewModel),
            Customizable = false
        };
    }
}
