using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Provides Alice DeRoss guide avatar assets for the built-in Product Tour.
/// </summary>
public sealed class AliceTutorialAvatarProvider : ITutorialAvatarProvider
{
    private readonly ISettingsHostService _settingsHostService;
    private readonly IReadOnlyDictionary<TutorialAvatarPose, ImageSource?> _images;

    /// <summary>
    /// Initializes a new instance of the <see cref="AliceTutorialAvatarProvider"/> class.
    /// </summary>
    /// <param name="settingsHostService">Settings host service used to resolve the current language.</param>
    public AliceTutorialAvatarProvider(ISettingsHostService settingsHostService)
    {
        _settingsHostService = settingsHostService;
        _images = new Dictionary<TutorialAvatarPose, ImageSource?>
        {
            [TutorialAvatarPose.Idle] = LoadImage("idle.png"),
            [TutorialAvatarPose.LeftTop] = LoadImage("lt.png"),
            [TutorialAvatarPose.LeftBottom] = LoadImage("lb.png"),
            [TutorialAvatarPose.RightTop] = LoadImage("rt.png"),
            [TutorialAvatarPose.RightBottom] = LoadImage("rb.png")
        };
    }

    /// <inheritdoc />
    public TutorialAvatar? GetAvatar(TutorialAvatarPose pose)
    {
        if (!_images.TryGetValue(pose, out var image))
        {
            image = _images[TutorialAvatarPose.Idle];
        }

        return new TutorialAvatar
        {
            DisplayName = GetDisplayName(),
            ImageSource = image
        };
    }

    private string GetDisplayName()
    {
        return _settingsHostService.Settings.Language switch
        {
            LanguageKey.zh_Hans => "爱丽丝·德罗斯",
            LanguageKey.en_US => "Alice DeRoss",
            LanguageKey.ja_JP => "アリス・デロス",
            _ => GetSystemLanguageDisplayName(_settingsHostService.Settings.CultureInfo)
        };
    }

    private static string GetSystemLanguageDisplayName(CultureInfo culture)
    {
        if (culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "爱丽丝·德罗斯";
        }

        if (culture.Name.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return "アリス・デロス";
        }

        return "Alice DeRoss";
    }

    private static ImageSource? LoadImage(string fileName)
    {
        //var image = new BitmapImage();
        //image.BeginInit();
        //image.UriSource = new Uri($"pack://application:,,,/neo-bpsys-wpf;component/Resources/Alice/{fileName}", UriKind.Absolute);
        //image.CacheOption = BitmapCacheOption.OnLoad;
        //image.EndInit();
        //image.Freeze();
        var image = ImageHelper.GetImageSourceFromFileName("Alice", fileName);
        return image;
    }
}
