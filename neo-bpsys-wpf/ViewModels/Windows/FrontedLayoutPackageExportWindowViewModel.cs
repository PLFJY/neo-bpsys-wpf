using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class FrontedLayoutPackageExportWindowViewModel : ViewModelBase
{
    private static readonly Regex UnsafePackageIdChars = new("[^a-z0-9._-]+", RegexOptions.Compiled);

    private readonly IFilePickerService? _filePickerService;

    public FrontedLayoutPackageExportWindowViewModel()
        : this(null)
    {
    }

    public FrontedLayoutPackageExportWindowViewModel(IFilePickerService? filePickerService)
    {
        _filePickerService = filePickerService;
        Author = GetDefaultAuthor();
        MinVersion = GetDefaultMinVersion();
        PackageId = CreateDefaultPackageId(Author);
        PackageName = I18nHelper.GetLocalizedString("FrontendLayoutPackageDefaultName");
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
    private string _packageId = string.Empty;

    [ObservableProperty]
    private string _packageName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _author = string.Empty;

    [ObservableProperty]
    private string _minVersion = string.Empty;

    [ObservableProperty]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private FrontedLayoutPackageExportScopeOption? _selectedScopeOption;

    [RelayCommand]
    private void BrowseOutputPath()
    {
        if (_filePickerService is null)
        {
            return;
        }

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

    private static string GetDefaultAuthor()
    {
        try
        {
            return Environment.UserName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetDefaultMinVersion()
    {
        var appVersion = NormalizeVersion(AppConstants.AppVersion);
        if (!string.IsNullOrWhiteSpace(appVersion))
        {
            return appVersion;
        }

        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
               ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
               ?? "3.0.0";
    }

    private static string? NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)
            || string.Equals(version, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = version.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOfAny(['+', '-']);
        if (metadataIndex > 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return Version.TryParse(normalized, out var parsed)
            ? parsed.ToString(parsed.Build >= 0 ? 3 : 2)
            : null;
    }

    private static string CreateDefaultPackageId(string author)
    {
        var name = string.IsNullOrWhiteSpace(author) ? "user" : author;
        var safeName = UnsafePackageIdChars.Replace(name.Trim().ToLowerInvariant(), "-")
            .Replace("..", "-", StringComparison.Ordinal)
            .Trim('.', '-', '_');
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "user";
        }

        return $"{safeName}.layout.{DateTime.Now:yyyyMMddHHmm}";
    }
}

public sealed record FrontedLayoutPackageExportScopeOption(
    FrontedLayoutPackageExportScope Scope,
    string DisplayName,
    bool IsEnabled);
