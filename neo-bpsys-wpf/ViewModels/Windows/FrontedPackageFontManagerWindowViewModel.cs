using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.ViewModels.Windows;

/// <summary>
/// 活动布局包字体管理器窗口的 ViewModel。
/// </summary>
public partial class FrontedPackageFontManagerWindowViewModel : ViewModelBase
{
    private readonly FrontedPackageFontManager _fontManager;
    private readonly FrontedFontFamilyOptionProvider _fontFamilyOptionProvider;

    /// <summary>
    /// 初始化设计时包字体管理器视图模型。
    /// </summary>
    public FrontedPackageFontManagerWindowViewModel()
        : this(null!, new FrontedFontFamilyOptionProvider())
    {
    }

    /// <summary>
    /// 初始化包字体管理器视图模型。
    /// </summary>
    /// <param name="fontManager">包字体管理器。</param>
    /// <param name="fontFamilyOptionProvider">字体族选项提供程序。</param>
    public FrontedPackageFontManagerWindowViewModel(
        FrontedPackageFontManager fontManager,
        FrontedFontFamilyOptionProvider fontFamilyOptionProvider)
    {
        _fontManager = fontManager;
        _fontFamilyOptionProvider = fontFamilyOptionProvider;
    }

    /// <summary>
    /// 获取活动包字体文件集合。
    /// </summary>
    public ObservableCollection<FrontedPackageFontItem> Fonts { get; } = [];

    /// <summary>
    /// 获取活动包是否没有已导入的字体。
    /// </summary>
    public bool HasNoFonts => Fonts.Count == 0;

    /// <summary>
    /// 获取当前选中的字体是否可被删除。
    /// </summary>
    public bool CanDeleteSelectedFont => SelectedFont?.CanDelete == true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedFont))]
    public partial FrontedPackageFontItem? SelectedFont { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    /// <summary>
    /// 加载当前活动包字体列表。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Fonts.Clear();
        if (_fontManager is null)
        {
            StatusText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Editor.PackageFontsUnavailable");
            OnPropertyChanged(nameof(HasNoFonts));
            return;
        }

        foreach (var font in await _fontManager.ListActivePackageFontsAsync(cancellationToken))
        {
            Fonts.Add(font);
        }

        SelectedFont = Fonts.FirstOrDefault();
        StatusText = HasNoFonts
            ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Editor.PackageFontsEmpty")
            : string.Empty;
        OnPropertyChanged(nameof(HasNoFonts));
    }

    /// <summary>
    /// 删除选中的未被引用的包字体文件。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>是否删除了字体。</returns>
    public async Task<bool> DeleteSelectedFontAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedFont is not { CanDelete: true } font || _fontManager is null)
        {
            StatusText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Editor.PackageFontInUse");
            return false;
        }

        try
        {
            await _fontManager.DeleteActivePackageFontAsync(font.FileName, cancellationToken);
            _fontFamilyOptionProvider.ClearCache();
            await LoadAsync(cancellationToken);
            StatusText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Editor.PackageFontDeleteSucceeded");
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Editor.PackageFontDeleteFailed")}: {ex.Message}";
            return false;
        }
    }
}
