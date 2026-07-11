#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Text;
using System.Xml.Linq;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Tests.Infrastructure;
using WPFLocalizeExtension.Engine;
using WPFLocalizeExtension.Providers;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Audit tests validating the post-migration state of the i18n resource split
/// from the monolithic Locales/Lang*.resx family into feature-owned resource families.
/// </summary>
public sealed class I18nResourceAuditTest
{
    private const string HostAssembly = "neo-bpsys-wpf";
    private const string SmartBpModuleAssembly = "neo-bpsys-wpf.SmartBp.Module";

    private static readonly string[] HostFamilyNames =
    {
        "Common", "Shell", "Team", "Game", "Bp", "Score",
        "FrontManage", "Designer", "AnimationEditor", "Settings", "PluginMarket"
    };

    private static readonly string[] HostDictionaryNames =
    {
        "Locales.Common", "Locales.Shell", "Locales.Team", "Locales.Game",
        "Locales.Bp", "Locales.Score", "Locales.FrontManage", "Locales.Designer",
        "Locales.AnimationEditor", "Locales.Settings", "Locales.PluginMarket"
    };

    private static readonly string[] SharedSmartBpKeys =
    {
        "Refresh", "Start", "Stop", "Delete", "Cancel", "SmartBp", "SaveSuccessfullyTo"
    };

    private static readonly HashSet<string> PostMigrationLocalizedKeys = new(StringComparer.Ordinal)
    {
        "ResetDefaultColor"
    };

    private static readonly Dictionary<string, Dictionary<string, ResourceEntry>> ResxCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IReadOnlyList<ResourceEntry>> BaselineCache = new(StringComparer.Ordinal);
    private static List<KeyMapEntry>? _keyMapCache;

    // ---------------------------------------------------------------------
    // 9.1 Migration-integrity tests
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies that the total number of data rows in key-map.csv equals the
    /// documented original Lang.resx neutral key count, and that the sum of
    /// host family neutral keys equals the number of key-map entries whose
    /// TargetAssembly is the host assembly.
    /// </summary>
    [Fact]
    public void TotalNeutralKeyCountMatchesKeyMap()
    {
        var keyMap = LoadKeyMap();

        var baselineKeys = LoadBaseline("neutral").Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var addedKeys = keyMap.Where(entry => string.IsNullOrWhiteSpace(entry.SourceDictionary)).ToArray();
        Assert.Equal(LoadBaseline("neutral").Count + addedKeys.Length, keyMap.Count);
        Assert.All(addedKeys, entry => Assert.DoesNotContain(entry.Key, baselineKeys));

        var hostKeyMapEntryCount = keyMap.Count(e => e.TargetAssembly == HostAssembly);
        var hostNeutralKeySum = HostFamilyNames
            .Sum(family => LoadResxKeys(GetHostNeutralResxPath(family)).Count);

        Assert.Equal(hostKeyMapEntryCount, hostNeutralKeySum);
    }

    /// <summary>
    /// Verifies that every key listed in key-map.csv exists in its target
    /// dictionary's neutral resx file.
    /// </summary>
    [Fact]
    public void EveryKeyMapEntryExistsInTargetDictionary()
    {
        var keyMap = LoadKeyMap();
        var resxCache = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var entry in keyMap)
        {
            var cacheKey = entry.TargetAssembly + "|" + entry.TargetDictionary;
            if (!resxCache.TryGetValue(cacheKey, out var keys))
            {
                var path = GetNeutralResxPath(entry.TargetAssembly, entry.TargetDictionary);
                keys = new HashSet<string>(LoadResxKeys(path).Keys, StringComparer.Ordinal);
                resxCache[cacheKey] = keys;
            }

            Assert.True(
                keys.Contains(entry.Key),
                $"Key '{entry.Key}' from key-map.csv was not found in {entry.TargetAssembly}/{entry.TargetDictionary}.resx");
        }
    }

    /// <summary>
    /// Verifies that every key present in a host family's en-us or ja-jp resx
    /// file has a corresponding neutral key in the same family.
    /// </summary>
    [Fact]
    public void NoLocalizedOnlyKeysInHostFamilies()
    {
        foreach (var family in HostFamilyNames)
        {
            var neutralKeys = LoadResxKeys(GetHostNeutralResxPath(family)).Keys
                .ToHashSet(StringComparer.Ordinal);

            var enUsPath = GetRepositoryPath("neo-bpsys-wpf", "Locales", family + ".en-us.resx");
            if (File.Exists(enUsPath))
            {
                var enUsKeys = LoadResxKeys(enUsPath).Keys;
                foreach (var key in enUsKeys)
                {
                    Assert.True(
                        neutralKeys.Contains(key),
                        $"en-us key '{key}' in {family} has no neutral counterpart");
                }
            }

            var jaJpPath = GetRepositoryPath("neo-bpsys-wpf", "Locales", family + ".ja-jp.resx");
            if (File.Exists(jaJpPath))
            {
                var jaJpKeys = LoadResxKeys(jaJpPath).Keys;
                foreach (var key in jaJpKeys)
                {
                    Assert.True(
                        neutralKeys.Contains(key),
                        $"ja-jp key '{key}' in {family} has no neutral counterpart");
                }
            }
        }
    }

    /// <summary>
    /// Verifies that the union of all host family neutral keys equals the set
    /// of keys in key-map.csv whose TargetAssembly is the host assembly,
    /// ensuring no host key was lost during migration.
    /// </summary>
    [Fact]
    public void NoHostKeyLostInMigration()
    {
        var keyMap = LoadKeyMap();
        var hostKeyMapKeys = keyMap
            .Where(e => e.TargetAssembly == HostAssembly)
            .Select(e => e.Key)
            .ToHashSet(StringComparer.Ordinal);

        var hostNeutralKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var family in HostFamilyNames)
        {
            foreach (var key in LoadResxKeys(GetHostNeutralResxPath(family)).Keys)
            {
                hostNeutralKeys.Add(key);
            }
        }

        Assert.Equal(hostKeyMapKeys.Count, hostNeutralKeys.Count);
        Assert.True(hostKeyMapKeys.SetEquals(hostNeutralKeys));
    }

    // ---------------------------------------------------------------------
    // 9.2 Dictionary-ownership tests
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies that no key appears in two different host family neutral
    /// resx files (each host key has exactly one owning dictionary).
    /// </summary>
    [Fact]
    public void EachHostNeutralKeyHasExactlyOneOwner()
    {
        var keyToOwners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var family in HostFamilyNames)
        {
            foreach (var key in LoadResxKeys(GetHostNeutralResxPath(family)).Keys)
            {
                if (!keyToOwners.TryGetValue(key, out var owners))
                {
                    owners = new List<string>();
                    keyToOwners[key] = owners;
                }

                owners.Add(family);
            }
        }

        var duplicates = keyToOwners
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value)}")
            .ToList();

        Assert.Empty(duplicates);
    }

    /// <summary>
    /// Verifies that every host family neutral resx file contains at least
    /// one key (no empty host dictionaries).
    /// </summary>
    [Fact]
    public void NoEmptyHostDictionary()
    {
        foreach (var family in HostFamilyNames)
        {
            var keys = LoadResxKeys(GetHostNeutralResxPath(family));
            Assert.True(keys.Count > 0, $"Host dictionary {family}.resx should contain at least one key");
        }
    }

    /// <summary>
    /// Verifies that no key in Common.resx appears in any other host family
    /// neutral resx file.
    /// </summary>
    [Fact]
    public void CommonKeysNotDuplicatedInFeatureDicts()
    {
        var commonKeys = LoadResxKeys(GetHostNeutralResxPath("Common")).Keys
            .ToHashSet(StringComparer.Ordinal);

        var duplicates = new List<string>();
        foreach (var family in HostFamilyNames)
        {
            if (family == "Common")
            {
                continue;
            }

            foreach (var key in LoadResxKeys(GetHostNeutralResxPath(family)).Keys)
            {
                if (commonKeys.Contains(key))
                {
                    duplicates.Add($"{key}: in both Common and {family}");
                }
            }
        }

        Assert.Empty(duplicates);
    }

    // ---------------------------------------------------------------------
    // 9.3 Resource-family-structure tests
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies that for every *.en-us.resx or *.ja-jp.resx file in the host
    /// Locales directory, the corresponding neutral *.resx file exists.
    /// </summary>
    [Fact]
    public void EveryCultureFileHasNeutralCounterpart()
    {
        var localesDir = GetRepositoryPath("neo-bpsys-wpf", "Locales");
        var resxFiles = Directory.GetFiles(localesDir, "*.resx");

        foreach (var file in resxFiles)
        {
            var fileName = Path.GetFileName(file);
            string? neutralFileName = null;

            if (fileName.EndsWith(".en-us.resx", StringComparison.OrdinalIgnoreCase))
            {
                neutralFileName = fileName.Substring(0, fileName.Length - ".en-us.resx".Length) + ".resx";
            }
            else if (fileName.EndsWith(".ja-jp.resx", StringComparison.OrdinalIgnoreCase))
            {
                neutralFileName = fileName.Substring(0, fileName.Length - ".ja-jp.resx".Length) + ".resx";
            }

            if (neutralFileName != null)
            {
                var neutralPath = Path.Combine(Path.GetDirectoryName(file)!, neutralFileName);
                Assert.True(
                    File.Exists(neutralPath),
                    $"Neutral file '{neutralFileName}' not found for '{fileName}'");
            }
        }
    }

    /// <summary>
    /// Verifies that no resx file in the host Locales directory has an
    /// unexpected culture suffix (only neutral, .en-us, .ja-jp are allowed).
    /// </summary>
    [Fact]
    public void OnlySupportedCultureSuffixes()
    {
        var localesDir = GetRepositoryPath("neo-bpsys-wpf", "Locales");
        var resxFiles = Directory.GetFiles(localesDir, "*.resx");
        var supportedCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "en-us", "ja-jp" };

        foreach (var file in resxFiles)
        {
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            var lastDotIndex = nameWithoutExtension.LastIndexOf('.');
            if (lastDotIndex >= 0)
            {
                var culture = nameWithoutExtension.Substring(lastDotIndex + 1);
                Assert.True(
                    supportedCultures.Contains(culture),
                    $"Unexpected culture suffix '{culture}' in file '{Path.GetFileName(file)}'");
            }
        }
    }

    /// <summary>
    /// Verifies that all resx files in the host Locales directory parse as
    /// valid XML with a root element and at least one data child element.
    /// </summary>
    [Fact]
    public void AllHostResxFilesAreValidXml()
    {
        var localesDir = GetRepositoryPath("neo-bpsys-wpf", "Locales");
        var resxFiles = Directory.GetFiles(localesDir, "*.resx");

        Assert.NotEmpty(resxFiles);

        foreach (var file in resxFiles)
        {
            var doc = XDocument.Load(file);
            Assert.NotNull(doc.Root);
            Assert.Equal("root", doc.Root!.Name.LocalName);
            Assert.NotEmpty(doc.Root!.Elements("data"));
        }
    }

    // ---------------------------------------------------------------------
    // 9.4 Lookup tests
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies that I18nHelper.GetLocalizedString throws ArgumentException
    /// when the dictionary argument is null or whitespace.
    /// </summary>
    [Fact]
    public void HelperRejectsNullDictionary()
    {
        Assert.ThrowsAny<ArgumentException>(() => I18nHelper.GetLocalizedString(null!, "Zoom"));
        Assert.ThrowsAny<ArgumentException>(() => I18nHelper.GetLocalizedString("   ", "Zoom"));
    }

    /// <summary>
    /// Verifies that an absent localization key safely degrades to empty text.
    /// </summary>
    [Fact]
    public void HelperReturnsEmptyForNullOrWhitespaceKey()
    {
        Assert.Equal(string.Empty, I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, null!));
        Assert.Equal(string.Empty, I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "   "));
        Assert.Equal(
            string.Empty,
            I18nHelper.GetLocalizedString(
                AppI18nDictionaries.Shell,
                string.Empty,
                CultureInfo.GetCultureInfo("en-US")));
    }

    /// <summary>
    /// Verifies that a known feature key exists with a non-empty value in the
    /// corresponding feature dictionary's neutral resx file, confirming the
    /// migration placed the key in the correct dictionary.
    /// </summary>
    [Fact]
    public void HelperResolvesKnownFeatureKey()
    {
        var resxKeys = LoadResxKeys(GetHostNeutralResxPath("Designer"));
        Assert.True(resxKeys.ContainsKey("Zoom"), "Zoom key should exist in Designer.resx");
        Assert.False(string.IsNullOrWhiteSpace(resxKeys["Zoom"].Value),
            "Zoom value should not be empty in Designer.resx");
    }

    /// <summary>
    /// Verifies that every migration row documents an explicit ownership decision
    /// instead of falling back to the shell for ambiguous or unreferenced keys.
    /// </summary>
    [Fact]
    public void KeyMapDoesNotContainDefaultShellFallbackReasons()
    {
        var fallbackRows = LoadKeyMap()
            .Where(entry => entry.MappingReason.Contains("ambiguous", StringComparison.OrdinalIgnoreCase)
                            || entry.MappingReason.Contains("Shell default", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Key)
            .ToArray();

        Assert.Empty(fallbackRows);
    }

    [Fact]
    public void I18nMigration_EveryBaselineNeutralEntryShouldBePreservedExactly()
    {
        AssertBaselineCultureIsPreservedExactly("neutral");
    }

    [Fact]
    public void I18nMigration_EveryBaselineEnglishEntryShouldBePreservedExactly()
    {
        AssertBaselineCultureIsPreservedExactly("en-us");
    }

    [Fact]
    public void I18nMigration_EveryBaselineJapaneseEntryShouldBePreservedExactly()
    {
        AssertBaselineCultureIsPreservedExactly("ja-jp");
    }

    [Fact]
    public void I18nMigration_ShouldPreserveComments()
    {
        Assert.All(LoadBaseline("neutral"), entry =>
        {
            var actual = LoadMappedTargetEntry(entry, "neutral");
            Assert.Equal(entry.Comment, actual.Comment);
        });
    }

    [Fact]
    public void I18nMigration_ShouldPreserveXmlSpace()
    {
        Assert.All(LoadBaseline("neutral"), entry =>
        {
            var actual = LoadMappedTargetEntry(entry, "neutral");
            Assert.Equal(entry.XmlSpacePreserve, actual.XmlSpacePreserve);
        });
    }

    [Fact]
    public void I18nMigration_ShouldNotInventLocalizedEntries()
    {
        AssertNoLocalizedCoverageWasInvented("en-us");
        AssertNoLocalizedCoverageWasInvented("ja-jp");
    }

    [Fact]
    public void I18nMigration_ShouldNotLoseLocalizedEntries()
    {
        AssertBaselineCultureIsPreservedExactly("en-us");
        AssertBaselineCultureIsPreservedExactly("ja-jp");
    }

    [Fact]
    public void I18nMigration_EveryKeyShouldHaveExactlyOneTargetOwner()
    {
        var duplicates = LoadKeyMap()
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => $"{group.Key}: {group.Count()} owners")
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void I18nMigration_KeyMapShouldMatchBaselineKeys()
    {
        var baselineKeys = LoadBaseline("neutral").Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var keyMap = LoadKeyMap();
        var migratedKeys = keyMap
            .Where(entry => !string.IsNullOrWhiteSpace(entry.SourceDictionary))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(baselineKeys.SetEquals(migratedKeys),
            "Tracked baseline keys must all have exactly one migration target; newly authored keys use an empty SourceDictionary.");
    }

    /// <summary>
    /// Verifies that the WPF localization provider resolves a resource through
    /// the dictionary base name emitted into the host assembly. This protects
    /// XAML <c>lex:Loc</c> bindings from silently degrading to <c>Key: ...</c>.
    /// </summary>
    [Fact]
    public void XamlProviderResolvesHostResource()
    {
        WpfTestThread.Run(() =>
        {
            var root = new Grid();
            ResxLocalizationProvider.SetDefaultAssembly(root, HostAssembly);
            ResxLocalizationProvider.SetDefaultDictionary(root, "neo_bpsys_wpf.Locales.Shell");

            var value = LocalizeDictionary.Instance.GetLocalizedObject(
                "Backend",
                root,
                CultureInfo.GetCultureInfo("zh-CN"));

            Assert.Equal("后台管理", value);
        });
    }

    [Fact]
    public void WpfLocalization_SwitchingCultureShouldUpdateLiveLocalizedProperty()
    {
        WpfTestThread.Run(() =>
        {
            var previousCulture = LocalizeDictionary.Instance.Culture;
            try
            {
                var text = (TextBlock)XamlReader.Parse(
                    "<TextBlock xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                    "xmlns:lex=\"http://wpflocalizeextension.codeplex.com\" " +
                    "lex:ResxLocalizationProvider.DefaultAssembly=\"neo-bpsys-wpf\" " +
                    "lex:ResxLocalizationProvider.DefaultDictionary=\"neo_bpsys_wpf.Locales.Shell\" " +
                    "Text=\"{lex:Loc Backend}\" />");

                LocalizeDictionary.Instance.Culture = CultureInfo.GetCultureInfo("zh-CN");
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var chinese = text.Text;

                LocalizeDictionary.Instance.Culture = CultureInfo.GetCultureInfo("en-US");
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var english = text.Text;

                Assert.Equal("后台管理", chinese);
                Assert.Equal("Backend", english);
                Assert.NotEqual(chinese, english);
            }
            finally
            {
                LocalizeDictionary.Instance.Culture = previousCulture;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }
        });
    }

    /// <summary>
    /// Verifies that I18nHelper returns the key itself when the key does not
    /// exist in the specified dictionary.
    /// </summary>
    [Fact]
    public void HelperReturnsKeyForUnknownKey()
    {
        const string unknownKey = "ThisKeyDoesNotExist_AuditTest_12345";

        var result = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, unknownKey);
        Assert.Equal(unknownKey, result);
    }

    /// <summary>
    /// Verifies that the neutral resx file provides a fallback for cultures
    /// without a culture-specific resx file: a key exists in the neutral
    /// Shell.resx, and no Shell.fr-FR.resx file exists, so the neutral file
    /// serves as the fallback source.
    /// </summary>
    [Fact]
    public void HelperFallsBackToNeutralCulture()
    {
        var neutralKeys = LoadResxKeys(GetHostNeutralResxPath("Shell"));
        Assert.NotEmpty(neutralKeys);

        var frFrPath = GetRepositoryPath("neo-bpsys-wpf", "Locales", "Shell.fr-FR.resx");
        Assert.False(File.Exists(frFrPath),
            "Shell.fr-FR.resx should not exist; the neutral file serves as fallback for unsupported cultures.");
    }

    // ---------------------------------------------------------------------
    // 9.5 Source-cleanup tests
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies that no XAML file in the host project (excluding tests and
    /// tools) contains a reference to the old Locales.Lang dictionary.
    /// </summary>
    [Fact]
    public void NoXamlReferencesLocalesLang()
    {
        var hostDir = GetRepositoryPath("neo-bpsys-wpf");
        var violations = ScanFilesForForbiddenStrings(hostDir, "*.xaml", new[] { "Locales.Lang" });
        Assert.Empty(violations);
    }

    /// <summary>
    /// Verifies that no C# file in the host project (excluding tests and
    /// tools) contains a reference to Locales.Lang or Lang.ResourceManager.
    /// </summary>
    [Fact]
    public void NoCSharpReferencesLocalesLangOrResourceManager()
    {
        var hostDir = GetRepositoryPath("neo-bpsys-wpf");
        var violations = ScanFilesForForbiddenStrings(
            hostDir,
            "*.cs",
            new[] { "Locales.Lang", "Lang.ResourceManager" });

        Assert.Empty(violations);
    }

    /// <summary>
    /// Verifies that no .csproj file in the repository (excluding tools)
    /// contains references to Lang.Designer or PublicResXFileCodeGenerator.
    /// </summary>
    [Fact]
    public void NoLangDesignerInCsproj()
    {
        var repoRoot = GetRepositoryRoot();
        var csprojFiles = Directory.GetFiles(repoRoot, "*.csproj", SearchOption.AllDirectories);

        var violations = new List<string>();
        foreach (var file in csprojFiles)
        {
            if (IsExcludedPath(file))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            if (content.Contains("Lang.Designer", StringComparison.Ordinal))
            {
                violations.Add($"{file}: contains 'Lang.Designer'");
            }

            if (content.Contains("PublicResXFileCodeGenerator", StringComparison.Ordinal))
            {
                violations.Add($"{file}: contains 'PublicResXFileCodeGenerator'");
            }
        }

        Assert.Empty(violations);
    }

    /// <summary>
    /// Verifies via reflection that the host I18nHelper does not expose a
    /// single-argument GetLocalizedString(string) overload (only the 2-arg
    /// and 3-arg overloads should exist after migration).
    /// </summary>
    [Fact]
    public void OldSingleArgHelperOverloadAbsent()
    {
        var method = typeof(I18nHelper).GetMethod(
            "GetLocalizedString",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            new[] { typeof(string) },
            modifiers: null);

        Assert.Null(method);
    }

    [Fact]
    public void AnyHostDictionaryLookup_ShouldOnlyBeUsedByFrontedLayoutLocalizationResolver()
    {
        var hostDir = GetRepositoryPath("neo-bpsys-wpf");
        var usages = Directory.GetFiles(hostDir, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsExcludedPath(path))
            .Where(path => File.ReadAllText(path).Contains("GetLocalizedStringFromAnyHostDictionary", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(GetRepositoryRoot(), path).Replace('\\', '/'))
            .Where(path => path is not "neo-bpsys-wpf/Helpers/I18nHelper.cs")
            .ToArray();

        Assert.Equal(
            ["neo-bpsys-wpf/Controls/FrontedLayout/LocalizedTextFrontedControl.cs"],
            usages);
    }

    // ---------------------------------------------------------------------
    // 9.6 Assembly-ownership tests
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies that no C# or XAML file in the ProductTour project references
    /// Locales.Lang or any host dictionary name (it should only reference
    /// Locales.Tour).
    /// </summary>
    [Fact]
    public void ProductTourHasNoHostLocalesDependency()
    {
        var tourDir = GetRepositoryPath("neo-bpsys-wpf.ProductTour");
        var forbidden = HostDictionaryNames.Append("Locales.Lang").ToList();

        var violations = new List<string>();
        foreach (var pattern in new[] { "*.cs", "*.xaml" })
        {
            foreach (var file in Directory.GetFiles(tourDir, pattern, SearchOption.AllDirectories))
            {
                if (IsExcludedPath(file))
                {
                    continue;
                }

                var content = File.ReadAllText(file);
                foreach (var forbiddenStr in forbidden)
                {
                    if (content.Contains(forbiddenStr, StringComparison.Ordinal))
                    {
                        violations.Add($"{file}: contains '{forbiddenStr}'");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    /// <summary>
    /// Verifies that SmartBp.resx exists in the SmartBp module's Locales
    /// directory, contains keys, and includes all shared self-containment
    /// keys (Refresh, Start, Stop, Delete, Cancel, SmartBp, SaveSuccessfullyTo).
    /// </summary>
    [Fact]
    public void SmartBpModuleOwnsResources()
    {
        var smartBpResxPath = GetRepositoryPath("neo-bpsys-wpf.SmartBp.Module", "Locales", "SmartBp.resx");
        Assert.True(File.Exists(smartBpResxPath), "SmartBp.resx should exist in the SmartBp module Locales directory");

        var keys = LoadResxKeys(smartBpResxPath);
        Assert.NotEmpty(keys);

        foreach (var sharedKey in SharedSmartBpKeys)
        {
            Assert.True(
                keys.ContainsKey(sharedKey),
                $"SmartBp.resx should contain shared key '{sharedKey}' for module self-containment");
        }
    }

    /// <summary>
    /// Verifies that no C# or XAML file in the SmartBp module project
    /// references host dictionary names (it should only reference
    /// Locales.SmartBp).
    /// </summary>
    [Fact]
    public void SmartBpModuleDoesNotReferenceHostDicts()
    {
        var moduleDir = GetRepositoryPath("neo-bpsys-wpf.SmartBp.Module");
        var forbidden = HostDictionaryNames.Append("Locales.Lang").ToList();

        var violations = new List<string>();
        foreach (var pattern in new[] { "*.cs", "*.xaml" })
        {
            foreach (var file in Directory.GetFiles(moduleDir, pattern, SearchOption.AllDirectories))
            {
                if (IsExcludedPath(file))
                {
                    continue;
                }

                var content = File.ReadAllText(file);
                foreach (var forbiddenStr in forbidden)
                {
                    if (content.Contains(forbiddenStr, StringComparison.Ordinal))
                    {
                        violations.Add($"{file}: contains '{forbiddenStr}'");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    // ---------------------------------------------------------------------
    // 9.7 STA/WPF culture-switch test
    // ---------------------------------------------------------------------

    /// <summary>
    /// Verifies that the neutral (zh-CN) and en-us resx files for the Shell
    /// dictionary contain at least one key with differing values, proving
    /// that a culture-switch from zh-CN to en-us would produce different
    /// localized text for XAML bindings.
    /// </summary>
    [Fact]
    public void LocalizedValueChangesOnCultureSwitch()
    {
        var neutralKeys = LoadResxKeys(GetHostNeutralResxPath("Shell"));
        var enUsKeys = LoadResxKeys(GetRepositoryPath("neo-bpsys-wpf", "Locales", "Shell.en-us.resx"));

        var differingKeys = neutralKeys
            .Where(kvp => enUsKeys.TryGetValue(kvp.Key, out var enEntry)
                          && !string.Equals(kvp.Value.Value, enEntry.Value, StringComparison.Ordinal))
            .Select(kvp => kvp.Key)
            .ToList();

        Assert.NotEmpty(differingKeys);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>
    /// Resolves the repository root from the test file location using
    /// CallerFilePath. The test file is in neo-bpsys-wpf.Tests/Services/,
    /// so going up two directories yields the repository root.
    /// </summary>
    /// <param name="sourceFilePath">Automatically supplied by the compiler.</param>
    /// <returns>The absolute path to the repository root.</returns>
    private static string GetRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", ".."));
    }

    /// <summary>
    /// Combines the repository root with the provided relative path parts.
    /// </summary>
    /// <param name="parts">Relative path segments to combine under the repository root.</param>
    /// <returns>The absolute path within the repository.</returns>
    private static string GetRepositoryPath(params string[] parts)
    {
        return Path.Combine(GetRepositoryRoot(), Path.Combine(parts));
    }

    private static string GetMigrationTestDataPath(string fileName)
    {
        return GetRepositoryPath("neo-bpsys-wpf.Tests", "TestData", "I18nMigration", fileName);
    }

    private static IReadOnlyList<ResourceEntry> LoadBaseline(string culture)
    {
        if (BaselineCache.TryGetValue(culture, out var cached))
        {
            return cached;
        }

        var path = GetMigrationTestDataPath(culture + ".json");
        var snapshot = JsonSerializer.Deserialize<BaselineSnapshot>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(snapshot);
        Assert.Equal(culture, snapshot!.Culture);
        var entries = snapshot.Entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
        BaselineCache[culture] = entries;
        return entries;
    }

    private static void AssertBaselineCultureIsPreservedExactly(string culture)
    {
        foreach (var expected in LoadBaseline(culture))
        {
            var actual = LoadMappedTargetEntry(expected, culture);
            Assert.True(
                expected.Equals(actual),
                $"Migration mismatch. Culture={culture}; Key={expected.Key}; Expected='{expected.Value}'; " +
                $"Actual='{actual.Value}'; ExpectedComment='{expected.Comment}'; ActualComment='{actual.Comment}'; " +
                $"ExpectedXmlSpace={expected.XmlSpacePreserve}; ActualXmlSpace={actual.XmlSpacePreserve}.");
        }
    }

    private static ResourceEntry LoadMappedTargetEntry(ResourceEntry expected, string culture)
    {
        var map = Assert.Single(LoadKeyMap().Where(entry => entry.Key == expected.Key));
        var path = GetTargetResxPath(map.TargetAssembly, map.TargetDictionary, culture);
        var targetEntries = LoadResxKeys(path);
        Assert.True(
            targetEntries.TryGetValue(expected.Key, out var actual),
            $"Missing migrated entry. Culture={culture}; Key={expected.Key}; TargetAssembly={map.TargetAssembly}; TargetDictionary={map.TargetDictionary}; File={path}");
        return actual!;
    }

    private static void AssertNoLocalizedCoverageWasInvented(string culture)
    {
        var neutralKeys = LoadBaseline("neutral").Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var localizedKeys = LoadBaseline(culture).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var maps = LoadKeyMap().ToDictionary(entry => entry.Key, StringComparer.Ordinal);

        foreach (var key in neutralKeys
                     .Except(localizedKeys, StringComparer.Ordinal)
                     .Except(PostMigrationLocalizedKeys, StringComparer.Ordinal))
        {
            var map = maps[key];
            var targetPath = GetTargetResxPath(map.TargetAssembly, map.TargetDictionary, culture);
            var targetEntries = LoadResxKeys(targetPath);
            Assert.False(
                targetEntries.ContainsKey(key),
                $"Migration invented localized entry. Culture={culture}; Key={key}; TargetAssembly={map.TargetAssembly}; TargetDictionary={map.TargetDictionary}");
        }
    }

    /// <summary>
    /// Loads all data entries from a resx file into a dictionary keyed by
    /// the data name attribute, with value and optional comment.
    /// </summary>
    /// <param name="path">Absolute path to the .resx file.</param>
    /// <returns>A dictionary of key to (value, comment) tuples.</returns>
    private static Dictionary<string, ResourceEntry> LoadResxKeys(string path)
    {
        if (ResxCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var doc = XDocument.Load(path);
        var result = new Dictionary<string, ResourceEntry>(StringComparer.Ordinal);

        foreach (var data in doc.Root!.Elements("data"))
        {
            var name = (string?)data.Attribute("name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            result[name] = new ResourceEntry(
                name,
                data.Element("value")?.Value ?? string.Empty,
                data.Element("comment")?.Value,
                string.Equals((string?)data.Attribute(XNamespace.Xml + "space"), "preserve", StringComparison.Ordinal));
        }

        ResxCache[path] = result;
        return result;
    }

    /// <summary>
    /// Gets the absolute path to a host family's neutral resx file.
    /// </summary>
    /// <param name="family">The host family name (e.g. "Shell").</param>
    /// <returns>The absolute path to the neutral .resx file.</returns>
    private static string GetHostNeutralResxPath(string family)
    {
        return GetRepositoryPath("neo-bpsys-wpf", "Locales", family + ".resx");
    }

    /// <summary>
    /// Resolves the neutral resx file path for a key-map entry's target
    /// assembly and target dictionary.
    /// </summary>
    /// <param name="targetAssembly">The target assembly name.</param>
    /// <param name="targetDictionary">The target dictionary (e.g. "Locales.AnimationEditor").</param>
    /// <returns>The absolute path to the neutral .resx file.</returns>
    /// <exception cref="ArgumentException">Thrown when the target assembly is unknown.</exception>
    private static string GetNeutralResxPath(string targetAssembly, string targetDictionary)
    {
        const string dictionaryPrefix = "Locales.";
        var family = targetDictionary.StartsWith(dictionaryPrefix, StringComparison.Ordinal)
            ? targetDictionary.Substring(dictionaryPrefix.Length)
            : targetDictionary;

        return targetAssembly switch
        {
            HostAssembly => GetRepositoryPath("neo-bpsys-wpf", "Locales", family + ".resx"),
            SmartBpModuleAssembly => GetRepositoryPath(
                "neo-bpsys-wpf.SmartBp.Module", "Locales", family + ".resx"),
            _ => throw new ArgumentException($"Unknown target assembly: {targetAssembly}")
        };
    }

    private static string GetTargetResxPath(string targetAssembly, string targetDictionary, string culture)
    {
        var neutralPath = GetNeutralResxPath(targetAssembly, targetDictionary);
        return culture == "neutral"
            ? neutralPath
            : Path.Combine(
                Path.GetDirectoryName(neutralPath)!,
                Path.GetFileNameWithoutExtension(neutralPath) + "." + culture + ".resx");
    }

    /// <summary>
    /// Loads and parses the key-map.csv file from the i18n-migration artifacts.
    /// </summary>
    /// <returns>A list of parsed key-map entries.</returns>
    private static List<KeyMapEntry> LoadKeyMap()
    {
        if (_keyMapCache is not null)
        {
            return _keyMapCache;
        }

        var path = GetMigrationTestDataPath("key-map.csv");
        var lines = File.ReadAllLines(path);
        var entries = new List<KeyMapEntry>();

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var fields = ParseCsvLine(lines[i]);
            if (fields.Length < 8)
            {
                continue;
            }

            entries.Add(new KeyMapEntry(
                fields[0],
                fields[1],
                fields[2],
                fields[3],
                int.Parse(fields[4], CultureInfo.InvariantCulture),
                fields[5],
                fields[6],
                bool.Parse(fields[7])));
        }

        _keyMapCache = entries;
        return entries;
    }

    /// <summary>
    /// Parses a single CSV line into fields, handling quoted fields that may
    /// contain embedded commas.
    /// </summary>
    /// <param name="line">The CSV line to parse.</param>
    /// <returns>An array of field values.</returns>
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }

    /// <summary>
    /// Scans files matching the given pattern in a directory tree for
    /// forbidden string content, excluding build output and tool directories.
    /// </summary>
    /// <param name="rootDir">The root directory to scan.</param>
    /// <param name="pattern">The file search pattern (e.g. "*.cs").</param>
    /// <param name="forbiddenStrings">Strings that must not appear in file contents.</param>
    /// <returns>A list of violation descriptions (file path and the forbidden string found).</returns>
    private static List<string> ScanFilesForForbiddenStrings(
        string rootDir,
        string pattern,
        IEnumerable<string> forbiddenStrings)
    {
        var forbiddenList = forbiddenStrings.ToList();
        var violations = new List<string>();

        foreach (var file in Directory.GetFiles(rootDir, pattern, SearchOption.AllDirectories))
        {
            if (IsExcludedPath(file))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            foreach (var forbiddenStr in forbiddenList)
            {
                if (content.Contains(forbiddenStr, StringComparison.Ordinal))
                {
                    violations.Add($"{file}: contains '{forbiddenStr}'");
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Determines whether a file path falls within an excluded directory
    /// (bin, obj, tools, artifacts).
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>True if the path is within an excluded directory; otherwise false.</returns>
    private static bool IsExcludedPath(string path)
    {
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var sep = Path.DirectorySeparatorChar.ToString();

        return normalized.Contains(sep + "bin" + sep, StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(sep + "obj" + sep, StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(sep + "tools" + sep, StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(sep + "artifacts" + sep, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Represents a single row in the key-map.csv migration artifact.
    /// </summary>
    /// <param name="Key">The resource key.</param>
    /// <param name="SourceDictionary">The original source dictionary (e.g. "Locales.Lang").</param>
    /// <param name="TargetAssembly">The target assembly name.</param>
    /// <param name="TargetDictionary">The target dictionary (e.g. "Locales.AnimationEditor").</param>
    /// <param name="ReferenceCount">The number of code references to this key.</param>
    /// <param name="ReferenceDomains">Semicolon-separated list of referencing files.</param>
    /// <param name="MappingReason">Human-readable reason for the mapping decision.</param>
    /// <param name="IsDynamic">Whether the key is resolved dynamically at runtime.</param>
    private sealed record KeyMapEntry(
        string Key,
        string SourceDictionary,
        string TargetAssembly,
        string TargetDictionary,
        int ReferenceCount,
        string ReferenceDomains,
        string MappingReason,
        bool IsDynamic);

    private sealed record BaselineSnapshot(string Culture, List<ResourceEntry> Entries);

    private sealed record ResourceEntry(
        string Key,
        string Value,
        string? Comment,
        bool XmlSpacePreserve);
}
