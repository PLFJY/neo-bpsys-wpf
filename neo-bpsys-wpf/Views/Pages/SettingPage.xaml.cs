using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// SettingPage.xaml 的交互逻辑
/// </summary>
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