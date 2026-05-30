using System.Windows.Controls;
using System.Windows.Data;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// ScorePage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("DC11039A-F03C-4A26-AD70-BAB9918B7D7D",
    "ScoreControl",
    SymbolRegular.NumberRow24,
    BackendPageCategory.Internal)]
public partial class ScorePage : Page
{
    public ScorePage()
    {
        InitializeComponent();
    }

    private void RestoreScorePreviewDefaultSort_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var view = CollectionViewSource.GetDefaultView(ScorePreviewDataGrid.ItemsSource);
        view?.SortDescriptions.Clear();

        foreach (var column in ScorePreviewDataGrid.Columns)
        {
            column.SortDirection = null;
        }

        view?.Refresh();
    }
}
