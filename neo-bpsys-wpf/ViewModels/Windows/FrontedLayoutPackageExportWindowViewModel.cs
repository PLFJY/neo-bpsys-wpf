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
    }

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

    partial void OnPackageIdChanged(string value) =>
        ClampManifestField(nameof(PackageId), value, v => PackageId = v);

    partial void OnPackageNameChanged(string value) =>
        ClampManifestField("Name", value, v => PackageName = v);

    partial void OnDescriptionChanged(string value) =>
        ClampManifestField(nameof(Description), value, v => Description = v);

    partial void OnAuthorChanged(string value) =>
        ClampManifestField(nameof(Author), value, v => Author = v);

    partial void OnMinVersionChanged(string value) =>
        ClampManifestField(nameof(MinVersion), value, v => MinVersion = v);

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
            PackageId = FrontedTextLimitHelper.Clamp(PackageId.Trim(), FrontedLayoutLimits.MaxPackageIdLength),
            Name = FrontedTextLimitHelper.Clamp(PackageName.Trim(), FrontedLayoutLimits.MaxPackageNameLength),
            Description = FrontedTextLimitHelper.Clamp(Description.Trim(), FrontedLayoutLimits.MaxPackageDescriptionLength),
            Author = FrontedTextLimitHelper.Clamp(Author.Trim(), FrontedLayoutLimits.MaxPackageAuthorLength),
            MinVersion = FrontedTextLimitHelper.Clamp(MinVersion.Trim(), FrontedLayoutLimits.MaxPackageMinVersionLength),
            ExportScope = FrontedLayoutPackageExportScope.AllFrontendLayouts,
            OutputPath = OutputPath.Trim()
        };
    }

    private void ClampManifestField(string fieldName, string value, Action<string> setValue)
    {
        var maxLength = FrontedTextLimitHelper.GetMaxLengthForManifestField(fieldName);
        var clamped = FrontedTextLimitHelper.Clamp(value, maxLength);
        if (!string.Equals(value, clamped, StringComparison.Ordinal))
        {
            setValue(clamped);
            ValidationMessage = I18nHelper.GetLocalizedString("InputTruncated");
        }
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

        if (FrontedTextLimitHelper.IsTooLong(OutputPath, FrontedLayoutLimits.MaxResourcePathLength))
        {
            return I18nHelper.GetLocalizedString("ResourcePathTooLong");
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
