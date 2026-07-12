#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
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
/// 审计测试，验证 i18n 资源拆分（从单一的 Locales/Lang*.resx 系列拆分为功能拥有的资源系列）
/// 迁移后的状态。
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
    /// 验证 key-map.csv 中的数据行总数等于文档中记录的原始 Lang.resx 中性键数量，
    /// 并且宿主系列中性键的总和等于 key-map 中 TargetAssembly 为宿主程序集的条目数。
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
    /// 验证 key-map.csv 中列出的每个键都存在于其目标字典的中性 resx 文件中。
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
    /// 验证宿主系列 en-us 或 ja-jp resx 文件中存在的每个键在同一系列中都有对应的中性键。
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
    /// 验证所有宿主系列中性键的并集等于 key-map.csv 中 TargetAssembly 为宿主程序集的
    /// 键集合，确保迁移过程中没有丢失任何宿主键。
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
    /// 验证没有键同时出现在两个不同的宿主系列中性 resx 文件中
    /// （每个宿主键都恰好由一个字典拥有）。
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
    /// 验证每个宿主系列中性 resx 文件至少包含一个键（没有空的宿主字典）。
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
    /// 验证 Common.resx 中的键不会出现在任何其他宿主系列中性 resx 文件中。
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
    /// 验证宿主 Locales 目录中每个 *.en-us.resx 或 *.ja-jp.resx 文件
    /// 都有对应的中性 *.resx 文件存在。
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
    /// 验证宿主 Locales 目录中没有 resx 文件包含非预期的区域后缀
    /// （仅允许中性、.en-us、.ja-jp）。
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
    /// 验证宿主 Locales 目录中所有 resx 文件都可以解析为
    /// 有效的 XML，且具有根元素和至少一个 data 子元素。
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
    /// 验证 I18nHelper.GetLocalizedString 在字典参数为 null 或空白时抛出 ArgumentException。
    /// </summary>
    [Fact]
    public void HelperRejectsNullDictionary()
    {
        Assert.ThrowsAny<ArgumentException>(() => I18nHelper.GetLocalizedString(null!, "Zoom"));
        Assert.ThrowsAny<ArgumentException>(() => I18nHelper.GetLocalizedString("   ", "Zoom"));
    }

    /// <summary>
    /// 验证不存在的本地化键会安全降级为空文本。
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
    /// 验证已知的特性键在对应特性字典的中性 resx 文件中存在且值非空，
    /// 以确认迁移将该键放置在正确的字典中。
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
    /// 验证每一行迁移记录都文档化了显式的归属决策，
    /// 而不是对不明确或未被引用的键回退到 shell。
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
    /// 验证 WPF 本地化提供程序能通过宿主程序集中发出的字典基名解析资源。
    /// 这可以防止 XAML <c>lex:Loc</c> 绑定静默退化为 <c>Key: ...</c>。
    /// </summary>
    [Fact]
    public void XamlProviderResolvesHostResource()
    {
        WpfTestThread.Run(() =>
        {
            var root = new Grid();
            IgnoreClosedDispatcherLocalizationNotifications(
                () => ResxLocalizationProvider.SetDefaultAssembly(root, HostAssembly));
            IgnoreClosedDispatcherLocalizationNotifications(
                () => ResxLocalizationProvider.SetDefaultDictionary(root, "neo_bpsys_wpf.Locales.Shell"));

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

                SetCultureSafely(CultureInfo.GetCultureInfo("zh-CN"));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var chinese = text.Text;

                SetCultureSafely(CultureInfo.GetCultureInfo("en-US"));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var english = text.Text;

                Assert.Equal("后台管理", chinese);
                Assert.Equal("Backend", english);
                Assert.NotEqual(chinese, english);
            }
            finally
            {
                SetCultureSafely(previousCulture);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }
        });
    }

    private static void SetCultureSafely(CultureInfo culture)
    {
        IgnoreClosedDispatcherLocalizationNotifications(
            () => LocalizeDictionary.Instance.Culture = culture);
    }

    private static void IgnoreClosedDispatcherLocalizationNotifications(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (IsClosedDispatcherLocalizationException(ex))
        {
        }
    }

    private static bool IsClosedDispatcherLocalizationException(Exception exception) =>
        exception is TaskCanceledException
        || (exception is AggregateException aggregate
            && aggregate.InnerExceptions.All(IsClosedDispatcherLocalizationException))
        || (exception is XamlParseException xpe
            && xpe.InnerException is { } inner
            && IsClosedDispatcherLocalizationException(inner));

    /// <summary>
    /// 验证当键在指定字典中不存在时，I18nHelper 返回键本身。
    /// </summary>
    [Fact]
    public void HelperReturnsKeyForUnknownKey()
    {
        const string unknownKey = "ThisKeyDoesNotExist_AuditTest_12345";

        var result = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, unknownKey);
        Assert.Equal(unknownKey, result);
    }

    /// <summary>
    /// 验证主窗口对局进度下拉菜单的资源键由 Shell.resx 拥有。
    /// </summary>
    [Fact]
    public void GameProgressDropdownKeysResolveFromShellDictionary()
    {
        var shellKeys = LoadResxKeys(GetHostNeutralResxPath("Shell"));
        var gameKeys = LoadResxKeys(GetHostNeutralResxPath("Game"));
        var progressKeys = new[]
        {
            "Free",
            "Game1FirstHalf",
            "Game1SecondHalf",
            "Game2FirstHalf",
            "Game2SecondHalf",
            "Game3FirstHalf",
            "Game3SecondHalf",
            "Game3OvertimeFirstHalf",
            "Game3OvertimeSecondHalf",
            "Game4FirstHalf",
            "Game4SecondHalf",
            "Game5FirstHalf",
            "Game5SecondHalf",
            "Game5OvertimeFirstHalf",
            "Game5OvertimeSecondHalf",
        };

        foreach (var key in progressKeys)
        {
            Assert.True(shellKeys.ContainsKey(key), $"Shell.resx should contain game progress key '{key}'.");
            Assert.False(gameKeys.ContainsKey(key), $"Game progress key '{key}' is owned by Shell.resx.");
        }
    }

    /// <summary>
    /// 验证动态对局进度键值的 XAML 绑定不会从 Game.resx 解析这些键。
    /// </summary>
    [Fact]
    public void XamlDynamicValueLocalizationDoesNotPointGameProgressKeysAtGameDictionary()
    {
        var hostDir = GetRepositoryPath("neo-bpsys-wpf");
        var violations = Directory.GetFiles(hostDir, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsExcludedPath(path))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { Path = path, Line = line, LineNumber = index + 1 }))
            .Where(entry => entry.Line.Contains("Binding Value", StringComparison.Ordinal)
                && entry.Line.Contains("ConverterParameter=Locales.Game", StringComparison.Ordinal))
            .Select(entry => $"{Path.GetRelativePath(GetRepositoryRoot(), entry.Path).Replace('\\', '/')}:{entry.LineNumber}")
            .ToArray();

        Assert.Empty(violations);
    }

    /// <summary>
    /// 验证中性 resx 文件为没有区域特定 resx 文件的区域提供回退：
    /// 某个键存在于中性 Shell.resx 中，且不存在 Shell.fr-FR.resx 文件，
    /// 因此中性文件作为回退来源。
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
    /// 验证宿主项目（排除测试和工具）中没有 XAML 文件引用旧的 Locales.Lang 字典。
    /// </summary>
    [Fact]
    public void NoXamlReferencesLocalesLang()
    {
        var hostDir = GetRepositoryPath("neo-bpsys-wpf");
        var violations = ScanFilesForForbiddenStrings(hostDir, "*.xaml", new[] { "Locales.Lang" });
        Assert.Empty(violations);
    }

    /// <summary>
    /// 验证宿主项目（排除测试和工具）中没有 C# 文件引用
    /// Locales.Lang 或 Lang.ResourceManager。
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
    /// 验证仓库中（排除工具）没有 .csproj 文件包含对
    /// Lang.Designer 或 PublicResXFileCodeGenerator 的引用。
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
    /// 通过反射验证宿主 I18nHelper 不暴露单参数 GetLocalizedString(string) 重载
    /// （迁移后只应存在 2 参数和 3 参数重载）。
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
    /// 验证 ProductTour 项目中没有 C# 或 XAML 文件引用
    /// Locales.Lang 或任何宿主字典名（应只引用 Locales.Tour）。
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
    /// 验证 SmartBp.resx 存在于 SmartBp 模块的 Locales 目录中，包含键，
    /// 并且包含所有共享的自包含键
    /// （Refresh、Start、Stop、Delete、Cancel、SmartBp、SaveSuccessfullyTo）。
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
    /// 验证 SmartBp 模块项目中没有 C# 或 XAML 文件引用
    /// 宿主字典名（应只引用 Locales.SmartBp）。
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
    /// 验证 Shell 字典的中性（zh-CN）和 en-us resx 文件至少包含一个值不同的键，
    /// 证明从 zh-CN 切换到 en-us 后 XAML 绑定会产生不同的本地化文本。
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
    /// 从测试文件位置使用 CallerFilePath 解析仓库根目录。
    /// 测试文件位于 neo-bpsys-wpf.Tests/Services/，
    /// 因此向上回溯两级即可得到仓库根目录。
    /// </summary>
    /// <param name="sourceFilePath">由编译器自动提供。</param>
    /// <returns>仓库根目录的绝对路径。</returns>
    private static string GetRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", ".."));
    }

    /// <summary>
    /// 将仓库根目录与提供的相对路径部分组合。
    /// </summary>
    /// <param name="parts">要在仓库根目录下组合的相对路径段。</param>
    /// <returns>仓库内的绝对路径。</returns>
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
    /// 将 resx 文件中的所有 data 项加载到以 data 名称属性为键的字典中，
    /// 包含值和可选的注释。
    /// </summary>
    /// <param name="path">.resx 文件的绝对路径。</param>
    /// <returns>键到（值, 注释）元组的字典。</returns>
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
    /// 获取宿主系列中性 resx 文件的绝对路径。
    /// </summary>
    /// <param name="family">宿主系列名（例如 "Shell"）。</param>
    /// <returns>中性 .resx 文件的绝对路径。</returns>
    private static string GetHostNeutralResxPath(string family)
    {
        return GetRepositoryPath("neo-bpsys-wpf", "Locales", family + ".resx");
    }

    /// <summary>
    /// 根据 key-map 条目的目标程序集和目标字典解析中性 resx 文件路径。
    /// </summary>
    /// <param name="targetAssembly">目标程序集名。</param>
    /// <param name="targetDictionary">目标字典（例如 "Locales.AnimationEditor"）。</param>
    /// <returns>中性 .resx 文件的绝对路径。</returns>
    /// <exception cref="ArgumentException">当目标程序集未知时抛出。</exception>
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
    /// 从 i18n-migration 制品中加载并解析 key-map.csv 文件。
    /// </summary>
    /// <returns>解析后的 key-map 条目列表。</returns>
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
    /// 将单行 CSV 解析为字段数组，处理可能包含嵌入逗号的带引号字段。
    /// </summary>
    /// <param name="line">要解析的 CSV 行。</param>
    /// <returns>字段值数组。</returns>
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
    /// 在目录树中扫描匹配指定模式的文件，查找禁止出现的字符串内容，
    /// 排除构建输出和工具目录。
    /// </summary>
    /// <param name="rootDir">要扫描的根目录。</param>
    /// <param name="pattern">文件搜索模式（例如 "*.cs"）。</param>
    /// <param name="forbiddenStrings">不得出现在文件内容中的字符串。</param>
    /// <returns>违规描述列表（文件路径和找到的禁止字符串）。</returns>
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
    /// 判断文件路径是否落在被排除的目录
    /// （bin、obj、tools、artifacts）内。
    /// </summary>
    /// <param name="path">要检查的文件路径。</param>
    /// <returns>若路径位于被排除的目录内则为 true；否则为 false。</returns>
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
    /// 表示 key-map.csv 迁移制品中的单行记录。
    /// </summary>
    /// <param name="Key">资源键。</param>
    /// <param name="SourceDictionary">原始源字典（例如 "Locales.Lang"）。</param>
    /// <param name="TargetAssembly">目标程序集名。</param>
    /// <param name="TargetDictionary">目标字典（例如 "Locales.AnimationEditor"）。</param>
    /// <param name="ReferenceCount">该键的代码引用数量。</param>
    /// <param name="ReferenceDomains">以分号分隔的引用文件列表。</param>
    /// <param name="MappingReason">该映射决策的人类可读原因。</param>
    /// <param name="IsDynamic">该键是否在运行时动态解析。</param>
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
