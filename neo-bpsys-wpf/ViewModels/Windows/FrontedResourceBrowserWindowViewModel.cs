using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class FrontedResourceBrowserWindowViewModel : ViewModelBase
{
    private readonly FrontedResourceBrowserProvider _provider;

    public FrontedResourceBrowserWindowViewModel()
        : this(new FrontedResourceBrowserProvider())
    {
    }

    public FrontedResourceBrowserWindowViewModel(FrontedResourceBrowserProvider provider)
    {
        _provider = provider;
        RefreshResources();
    }

    public IReadOnlyList<FrontedResourceBrowserItem> Resources { get; private set; } = [];

    public bool HasNoResults => Resources.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private FrontedResourceBrowserItem? _selectedResource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseSelected))]
    private string _selectedPath = string.Empty;

    public bool CanUseSelected => !string.IsNullOrWhiteSpace(SelectedPath);

    partial void OnSearchTextChanged(string value)
    {
        RefreshResources();
        OnPropertyChanged(nameof(HasNoResults));
    }

    partial void OnSelectedResourceChanged(FrontedResourceBrowserItem? value)
    {
        if (!string.IsNullOrWhiteSpace(value?.SelectedPath))
        {
            SelectedPath = value.SelectedPath;
        }
    }

    public void InitializeSelection(string? initialPath)
    {
        if (string.IsNullOrWhiteSpace(initialPath))
        {
            return;
        }

        SelectedPath = initialPath;
        SelectedResource = Resources.FirstOrDefault(resource =>
            string.Equals(resource.SelectedPath, initialPath, StringComparison.OrdinalIgnoreCase));
    }

    public void UseAbsoluteFile(string path)
    {
        var item = _provider.CreateAbsoluteFileItem(path);
        SelectedResource = item;
        SelectedPath = item.SelectedPath;
    }

    private void RefreshResources()
    {
        Resources = _provider.Search(SearchText);
        OnPropertyChanged(nameof(Resources));
    }
}
