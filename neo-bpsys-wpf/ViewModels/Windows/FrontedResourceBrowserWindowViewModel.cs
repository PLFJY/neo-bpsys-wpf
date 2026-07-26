using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class FrontedResourceBrowserWindowViewModel : ViewModelBase
{
    private readonly FrontedResourceBrowserProvider _provider;
    private readonly IFrontedImageSafetyService _imageSafetyService = new FrontedImageSafetyService();

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
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial FrontedResourceBrowserItem? SelectedResource { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseSelected))]
    public partial string SelectedPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ValidationMessage { get; set; } = string.Empty;

    public bool CanUseSelected => !string.IsNullOrWhiteSpace(SelectedPath);

    partial void OnSearchTextChanged(string value)
    {
        var clamped = FrontedTextLimitHelper.Clamp(value, FrontedLayoutLimits.MaxSearchTextLength);
        if (!string.Equals(value, clamped, StringComparison.Ordinal))
        {
            SearchText = clamped;
            return;
        }

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
        var validation = _imageSafetyService.ValidateFile(path, FrontedImagePurpose.UiElement);
        if (!validation.IsValid)
        {
            ValidationMessage = validation.ErrorCode ?? "InvalidImageResource";
            return;
        }

        var item = _provider.CreateAbsoluteFileItem(path);
        SelectedResource = item;
        SelectedPath = item.SelectedPath;
        ValidationMessage = string.Empty;
    }

    private void RefreshResources()
    {
        Resources = _provider.Search(SearchText);
        OnPropertyChanged(nameof(Resources));
    }
}
