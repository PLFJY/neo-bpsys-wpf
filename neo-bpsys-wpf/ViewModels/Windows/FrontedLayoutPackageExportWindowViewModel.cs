using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.IO;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class FrontedLayoutPackageExportWindowViewModel : ViewModelBase
{
    private readonly IFilePickerService _filePickerService;

    public FrontedLayoutPackageExportWindowViewModel()
        : this(null!)
    {
    }

    public FrontedLayoutPackageExportWindowViewModel(IFilePickerService filePickerService)
    {
        _filePickerService = filePickerService;
        ScopeOptions =
        [
            new FrontedLayoutPackageExportScopeOption(
                FrontedLayoutPackageExportScope.CurrentCanvas,
                I18nHelper.GetLocalizedString("ExportCurrentCanvas"),
                false),
            new FrontedLayoutPackageExportScopeOption(
                FrontedLayoutPackageExportScope.CurrentWindow,
                I18nHelper.GetLocalizedString("ExportCurrentWindow"),
                false),
            new FrontedLayoutPackageExportScopeOption(
                FrontedLayoutPackageExportScope.AllFrontendLayouts,
                I18nHelper.GetLocalizedString("ExportAllFrontendLayouts"),
                true)
        ];
        SelectedScopeOption = ScopeOptions.First(option => option.Scope == FrontedLayoutPackageExportScope.AllFrontendLayouts);
    }

    public IReadOnlyList<FrontedLayoutPackageExportScopeOption> ScopeOptions { get; }

    [ObservableProperty]
    private string _packageId = "my-layout-package";

    [ObservableProperty]
    private string _packageName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _author = string.Empty;

    [ObservableProperty]
    private string _minVersion = "3.0.0";

    [ObservableProperty]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private FrontedLayoutPackageExportScopeOption? _selectedScopeOption;

    [RelayCommand]
    private void BrowseOutputPath()
    {
        var defaultName = string.IsNullOrWhiteSpace(PackageId) ? "layout-package.bpui" : $"{PackageId}.bpui";
        var path = _filePickerService.SaveBpuiFile(defaultName);
        if (!string.IsNullOrWhiteSpace(path))
        {
            OutputPath = path;
        }
    }

    public FrontedLayoutPackageExportRequest? CreateRequest()
    {
        var error = Validate();
        if (!string.IsNullOrWhiteSpace(error))
        {
            ValidationMessage = error;
            return null;
        }

        return new FrontedLayoutPackageExportRequest
        {
            PackageId = PackageId.Trim(),
            Name = PackageName.Trim(),
            Description = Description.Trim(),
            Author = Author.Trim(),
            MinVersion = MinVersion.Trim(),
            ExportScope = SelectedScopeOption?.Scope ?? FrontedLayoutPackageExportScope.AllFrontendLayouts,
            OutputPath = OutputPath.Trim()
        };
    }

    private string Validate()
    {
        if (!FrontedLayoutPackageExporter.IsSafePackageId(PackageId.Trim()))
        {
            return I18nHelper.GetLocalizedString("InvalidPackageId");
        }

        if (string.IsNullOrWhiteSpace(PackageName))
        {
            return I18nHelper.GetLocalizedString("PackageNameRequired");
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            return I18nHelper.GetLocalizedString("OutputPathRequired");
        }

        if (!string.Equals(Path.GetExtension(OutputPath), ".bpui", StringComparison.OrdinalIgnoreCase))
        {
            OutputPath = Path.ChangeExtension(OutputPath, ".bpui");
        }

        return string.Empty;
    }
}

public sealed record FrontedLayoutPackageExportScopeOption(
    FrontedLayoutPackageExportScope Scope,
    string DisplayName,
    bool IsEnabled);
