using Microsoft.Win32;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedResourceBrowserWindow : FluentWindow
{
    public FrontedResourceBrowserWindow()
    {
        InitializeComponent();
    }

    public string? SelectedResourcePath { get; private set; }

    private FrontedResourceBrowserWindowViewModel? ViewModel =>
        DataContext as FrontedResourceBrowserWindowViewModel;

    public void InitializeSelection(string? initialPath) =>
        ViewModel?.InitializeSelection(initialPath);

    private void ResourceList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && e.AddedItems.Count > 0 && e.AddedItems[0] is FrontedResourceBrowserItem item)
        {
            ViewModel.SelectedResource = item;
        }
    }

    private void BrowseFile_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel?.UseAbsoluteFile(dialog.FileName);
        }
    }

    private void UseSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel?.SelectedPath))
        {
            return;
        }

        SelectedResourcePath = ViewModel.SelectedPath;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
