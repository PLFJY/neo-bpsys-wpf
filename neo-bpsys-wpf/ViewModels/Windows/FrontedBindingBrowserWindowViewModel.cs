using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class FrontedBindingBrowserWindowViewModel : ViewModelBase
{
    private readonly FrontedBindingBrowserProvider _provider;
    private readonly FrontedBindingTypeFilter _filter;
    private readonly IReadOnlyList<FrontedBindingTreeNode> _allNodes;

    public FrontedBindingBrowserWindowViewModel()
        : this(new FrontedBindingBrowserProvider(), FrontedBindingTypeFilter.Any)
    {
    }

    public FrontedBindingBrowserWindowViewModel(FrontedBindingBrowserProvider provider)
        : this(provider, FrontedBindingTypeFilter.Any)
    {
    }

    public FrontedBindingBrowserWindowViewModel(
        FrontedBindingBrowserProvider provider,
        FrontedBindingTypeFilter filter)
    {
        _provider = provider;
        _filter = filter;
        _allNodes = _provider.BuildTree(_filter);
        TreeNodes = _allNodes;
        RefreshSearchResults();
    }

    public IReadOnlyList<FrontedBindingTreeNode> TreeNodes { get; }

    public IReadOnlyList<FrontedBindingTreeNode> SearchResults { get; private set; } = [];

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    public bool HasNoResults => HasSearchText && SearchResults.Count == 0;

    public string ExpectedBindingTypeDisplay =>
        $"{I18nHelper.GetLocalizedString("ExpectedBindingType")}: "
        + I18nHelper.GetLocalizedString(_filter.DisplayNameKey);

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
        var clamped = FrontedTextLimitHelper.Clamp(value, FrontedLayoutLimits.MaxSearchTextLength);
        if (!string.Equals(value, clamped, StringComparison.Ordinal))
        {
            SearchText = clamped;
            return;
        }

        RefreshSearchResults();
        OnPropertyChanged(nameof(HasSearchText));
        OnPropertyChanged(nameof(HasNoResults));
    }

    partial void OnSelectedNodeChanged(FrontedBindingTreeNode? value)
    {
        if (value?.IsSelectable == true && !string.IsNullOrWhiteSpace(value.FullPath))
        {
            SelectedPath = value.FullPath;
            return;
        }

        SelectedPath = string.Empty;
    }

    public void InitializeSelection(string? initialPath)
    {
        if (string.IsNullOrWhiteSpace(initialPath))
        {
            return;
        }

        SelectedNode = _allNodes.SelectMany(node => node.Flatten())
            .FirstOrDefault(node => node.IsSelectable && string.Equals(node.FullPath, initialPath, StringComparison.Ordinal));
        if (SelectedNode is not null)
        {
            SelectedPath = initialPath;
        }
    }

    private void RefreshSearchResults()
    {
        SearchResults = _provider.Search(SearchText, _filter);
        OnPropertyChanged(nameof(SearchResults));
        OnPropertyChanged(nameof(HasNoResults));
    }
}
