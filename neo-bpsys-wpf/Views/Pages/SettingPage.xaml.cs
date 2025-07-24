using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Views.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// SettingPage.xaml 的交互逻辑
/// </summary>
public partial class SettingPage : Page
{
    public SettingPage(ITextSettingsNavigationService textSettingsNavigationService)
    {
        InitializeComponent();
        textSettingsNavigationService.SetFrameControl(FrontWindowType.BpWindow, BpWindowTextSettingFrame);
        textSettingsNavigationService.SetFrameControl(FrontWindowType.CutSceneWindow,
            CutSceneWindowTextSettingFrame);
        textSettingsNavigationService.SetFrameControl(FrontWindowType.ScoreGlobalWindow, ScoreWindowTextSettingFrame);
        textSettingsNavigationService.SetFrameControl(FrontWindowType.GameDataWindow,
            GameDataWindowTextSettingFrame);
        textSettingsNavigationService.SetFrameControl(FrontWindowType.WidgetsWindow, WidgetsWindowTextSettingFrame);
    }
}