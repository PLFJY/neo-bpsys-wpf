using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.ViewModels.Windows;

/// <summary>
/// ViewModel for the active layout package font manager window.
/// </summary>
public partial class FrontedPackageFontManagerWindowViewModel : ViewModelBase
{
    private readonly FrontedPackageFontManager _fontManager;
    private readonly FrontedFontFamilyOptionProvider _fontFamilyOptionProvider;

    /// <summary>
    /// Initializes a design-time package font manager view model.
    /// </summary>
    public FrontedPackageFontManagerWindowViewModel()
        : this(null!, new FrontedFontFamilyOptionProvider())
    {
    }

    /// <summary>
    /// Initializes a package font manager view model.
    /// </summary>
    /// <param name="fontManager">Package font manager.</param>
    /// <param name="fontFamilyOptionProvider">Font family option provider.</param>
    public FrontedPackageFontManagerWindowViewModel(
        FrontedPackageFontManager fontManager,
        FrontedFontFamilyOptionProvider fontFamilyOptionProvider)
    {
        _fontManager = fontManager;
        _fontFamilyOptionProvider = fontFamilyOptionProvider;
    }

    /// <summary>
    /// Gets the active package font files.
    /// </summary>
    public ObservableCollection<FrontedPackageFontItem> Fonts { get; } = [];

    /// <summary>
    /// Gets whether the active package has no imported fonts.
    /// </summary>
    public bool HasNoFonts => Fonts.Count == 0;

    /// <summary>
    /// Gets whether the selected font can be deleted.
    /// </summary>
    public bool CanDeleteSelectedFont => SelectedFont?.CanDelete == true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedFont))]
    public partial FrontedPackageFontItem? SelectedFont { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    /// <summary>
    /// Loads the current active package font list.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Fonts.Clear();
        if (_fontManager is null)
        {
            StatusText = I18nHelper.GetLocalizedString("Designer.Editor.PackageFontsUnavailable");
            OnPropertyChanged(nameof(HasNoFonts));
            return;
        }

        foreach (var font in await _fontManager.ListActivePackageFontsAsync(cancellationToken))
        {
            Fonts.Add(font);
        }

        SelectedFont = Fonts.FirstOrDefault();
        StatusText = HasNoFonts
            ? I18nHelper.GetLocalizedString("Designer.Editor.PackageFontsEmpty")
            : string.Empty;
        OnPropertyChanged(nameof(HasNoFonts));
    }

    /// <summary>
    /// Deletes the selected unreferenced package font file.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether a font was deleted.</returns>
    public async Task<bool> DeleteSelectedFontAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedFont is not { CanDelete: true } font || _fontManager is null)
        {
            StatusText = I18nHelper.GetLocalizedString("Designer.Editor.PackageFontInUse");
            return false;
        }

        try
        {
            await _fontManager.DeleteActivePackageFontAsync(font.FileName, cancellationToken);
            _fontFamilyOptionProvider.ClearCache();
            await LoadAsync(cancellationToken);
            StatusText = I18nHelper.GetLocalizedString("Designer.Editor.PackageFontDeleteSucceeded");
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"{I18nHelper.GetLocalizedString("Designer.Editor.PackageFontDeleteFailed")}: {ex.Message}";
            return false;
        }
    }
}
