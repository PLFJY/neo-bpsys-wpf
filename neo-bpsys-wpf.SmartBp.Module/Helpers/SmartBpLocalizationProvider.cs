using System.Collections.ObjectModel;
using System.Globalization;
using System.Resources;
using System.Windows;
using WPFLocalizeExtension.Providers;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// Resolves SmartBP resources from the exact runtime module assembly.
/// </summary>
/// <remarks>
/// The module is loaded in its own <see cref="System.Runtime.Loader.AssemblyLoadContext"/>.
/// The standard RESX provider resolves assemblies by name and can select or cache the wrong
/// context, so module views use this provider instead.
/// </remarks>
public sealed class SmartBpLocalizationProvider : ILocalizationProvider
{
    private const string AssemblyName = "neo-bpsys-wpf.SmartBp.Module";
    private const string DictionaryName = "Locales.SmartBp";
    private static readonly ResourceManager Resources = new(
        "neo_bpsys_wpf.Locales.SmartBp",
        typeof(SmartBpLocalizationProvider).Assembly);

    private SmartBpLocalizationProvider()
    {
        AvailableCultures =
        [
            CultureInfo.InvariantCulture,
            CultureInfo.GetCultureInfo("zh-CN"),
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("ja-JP")
        ];
    }

    public static SmartBpLocalizationProvider Instance { get; } = new();

    public ObservableCollection<CultureInfo> AvailableCultures { get; }

#pragma warning disable CS0067
    public event ProviderChangedEventHandler? ProviderChanged;
    public event ProviderErrorEventHandler? ProviderError;
    public event ValueChangedEventHandler? ValueChanged;
#pragma warning restore CS0067

    public FullyQualifiedResourceKeyBase GetFullyQualifiedResourceKey(string key, DependencyObject target) =>
        new FQAssemblyDictionaryKey(key, AssemblyName, DictionaryName);

    public object? GetLocalizedObject(string key, DependencyObject target, CultureInfo culture) =>
        GetString(key, culture);

    internal static string? GetString(string key, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        // A fully-qualified key may be supplied by Loc/BLoc. ResourceManager only needs
        // the final resource-name component.
        var separator = key.LastIndexOf(':');
        var resourceKey = separator >= 0 ? key[(separator + 1)..] : key;
        try
        {
            return Resources.GetString(resourceKey, culture);
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
        catch (MissingSatelliteAssemblyException)
        {
            return Resources.GetString(resourceKey, CultureInfo.InvariantCulture);
        }
    }
}
