using System.Windows.Controls;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

/// <summary>
/// FrontedWindowsView.xaml 的交互逻辑
/// </summary>
public partial class FrontedWindowsView : UserControl
{
    public FrontedWindowsView()
    {
        InitializeComponent();
        Loaded += (_, _) => TutorialPageLoader.RunPendingOnLoaded(this, TutorialPageKey, "Loaded");
        IsVisibleChanged += (_, e) =>
        {
            if (Equals(e.NewValue, true))
            {
                TutorialPageLoader.RunPendingOnLoaded(this, TutorialPageKey, "Visible");
            }
        };
    }
}
