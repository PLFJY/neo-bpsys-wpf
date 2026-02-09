using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// SettingPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("30B7A05B-027D-49FD-91F2-06272A281CF4", 
    "Settings",
    SymbolRegular.Settings24,
    BackendPageCategory.External)]
public partial class SettingPage : Page
{
    public SettingPage(ITextSettingsNavigationService textSettingsNavigationService)
    {
        InitializeComponent();
        textSettingsNavigationService.SetFrameControl(FrontedWindowType.BpWindow, BpWindowTextSettingFrame);
        textSettingsNavigationService.SetFrameControl(FrontedWindowType.CutSceneWindow,
            CutSceneWindowTextSettingFrame);
        textSettingsNavigationService.SetFrameControl(FrontedWindowType.ScoreGlobalWindow, ScoreWindowTextSettingFrame);
        textSettingsNavigationService.SetFrameControl(FrontedWindowType.GameDataWindow,
            GameDataWindowTextSettingFrame);
        textSettingsNavigationService.SetFrameControl(FrontedWindowType.WidgetsWindow, WidgetsWindowTextSettingFrame);
    }
}