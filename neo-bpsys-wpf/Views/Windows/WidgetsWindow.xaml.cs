using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// WidgetsWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("712D2E21-B8DF-4220-8E3D-8AD0003DD079", "WidgetsWindow",
    ["MapBpCanvas", "BpOverViewCanvas", "MapV2Canvas"], true)]
public partial class WidgetsWindow : FrontedWindowBase
{
    public WidgetsWindow()
    {
        InitializeComponent();
    }

}