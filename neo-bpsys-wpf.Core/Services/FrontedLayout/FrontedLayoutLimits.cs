namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public static class FrontedLayoutLimits
{
    public const long MaxManifestBytes = 256 * 1024;
    public const long MaxLayoutJsonBytes = 2 * 1024 * 1024;
    public const long MaxWindowOptionsJsonBytes = 64 * 1024;
    public const long MaxLegacyConfigBytes = 2 * 1024 * 1024;

    public const int MaxJsonDepth = 32;

    public const int WarningControlsPerCanvas = 160;
    public const int MaxControlsPerCanvas = 256;

    public const int MaxLayoutsPerPackage = 100;
    public const int MaxResourcesPerPackage = 500;

    public const int MaxControlNameLength = 64;
    public const int MaxControlTypeLength = 64;
    public const int MaxPackageIdLength = 128;
    public const int MaxPackageNameLength = 128;
    public const int MaxPackageAuthorLength = 64;
    public const int MaxPackageMinVersionLength = 32;
    public const int MaxPackageDescriptionLength = 2048;

    public const int MaxBindingPathLength = 256;
    public const int MaxResourcePathLength = 1024;
    public const int MaxFontFamilyLength = 256;
    public const int MaxStaticTextLength = 512;
    public const int MaxSearchTextLength = 128;
    public const int MaxValidationMessageLength = 512;
    public const int MaxValidationMessagesShown = 200;

    public const long MaxBackgroundImageBytes = 1 * 1024 * 1024;
    public const int MaxBackgroundImageLongSide = 4096;
    public const long MaxBackgroundImagePixels = 4096L * 4096L;

    public const long MaxUiImageBytes = 512 * 1024;
    public const int MaxUiImageLongSide = 2048;
    public const long MaxUiImagePixels = 2048L * 2048L;

    public const long MaxPackageArchiveBytes = 50 * 1024 * 1024;
    public const long MaxPackageExtractedBytes = 100 * 1024 * 1024;
    public const long MaxPackageSingleEntryBytes = 10 * 1024 * 1024;
    public const int MaxPackageEntries = 1000;
}
