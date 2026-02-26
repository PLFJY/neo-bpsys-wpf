using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.CoopBpPlugin.Views;

/// <summary>
/// 合作BP主页面
/// 提供房间创建、加入、客户端管理等功能的UI
/// </summary>
[BackendPageInfo(
    "E8F4A2B1-C5D3-4E7F-9A1B-2C3D4E5F6A7B",
    "合作BP",
    SymbolRegular.PeopleTeam24,
    BackendPageCategory.External)]
public partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }
}
