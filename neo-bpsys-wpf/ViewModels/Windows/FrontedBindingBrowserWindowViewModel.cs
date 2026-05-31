using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class FrontedBindingBrowserWindowViewModel : ViewModelBase
{
    private readonly FrontedBindingBrowserProvider _provider;
    private readonly IReadOnlyList<FrontedBindingTreeNode> _allNodes;

    public FrontedBindingBrowserWindowViewModel()
        : this(new FrontedBindingBrowserProvider())
    {
    }

    public FrontedBindingBrowserWindowViewModel(FrontedBindingBrowserProvider provider)
    {
        _provider = provider;
        _allNodes = _provider.BuildTree();
        TreeNodes = _allNodes;
        RefreshSearchResults();
    }

    public IReadOnlyList<FrontedBindingTreeNode> TreeNodes { get; }

    public IReadOnlyList<FrontedBindingTreeNode> SearchResults { get; private set; } = [];

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    public bool HasNoResults => HasSearchText && SearchResults.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private FrontedBindingTreeNode? _selectedNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseSelected))]
    private string _selectedPath = string.Empty;

    public bool CanUseSelected => !string.IsNullOrWhiteSpace(SelectedPath);

    partial void OnSearchTextChanged(string value)
    {
        RefreshSearchResults();
        OnPropertyChanged(nameof(HasSearchText));
        OnPropertyChanged(nameof(HasNoResults));
    }

    partial void OnSelectedNodeChanged(FrontedBindingTreeNode? value)
    {
        if (!string.IsNullOrWhiteSpace(value?.FullPath))
        {
            SelectedPath = value.FullPath;
        }
    }

    public void InitializeSelection(string? initialPath)
    {
        if (string.IsNullOrWhiteSpace(initialPath))
        {
            return;
        }

        SelectedPath = initialPath;
        SelectedNode = _allNodes.SelectMany(node => node.Flatten())
            .FirstOrDefault(node => string.Equals(node.FullPath, initialPath, StringComparison.Ordinal));
    }

    private void RefreshSearchResults()
    {
        SearchResults = _provider.Search(SearchText);
        OnPropertyChanged(nameof(SearchResults));
        OnPropertyChanged(nameof(HasNoResults));
    }
}
