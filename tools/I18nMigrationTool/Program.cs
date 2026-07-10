// Temporary deterministic migration tool for the i18n resource split.
// Parses .resx as XML (not regex), scans references, classifies keys, and
// generates split resource families + migration artifacts. The reusable audit
// logic lives separately in the test project; this tool performs the one-off
// migration and is deleted afterwards.
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using I18nMigrationTool;

// ---- Configuration ----------------------------------------------------------

var repoRoot = args.Length > 0
    ? args[0]
    : Directory.GetCurrentDirectory();

var mainProject = Path.Combine(repoRoot, "neo-bpsys-wpf");
var localesDir = Path.Combine(mainProject, "Locales");
var artifactsDir = Path.Combine(repoRoot, "artifacts", "i18n-migration");
var smartBpModuleDir = Path.Combine(repoRoot, "neo-bpsys-wpf.SmartBp.Module");
var productTourDir = Path.Combine(repoRoot, "neo-bpsys-wpf.ProductTour");

Directory.CreateDirectory(artifactsDir);

var cultures = new[] { "en-us", "ja-jp" };

// ---- Phase A: Parse resx files ----------------------------------------------

Console.WriteLine("Phase A: Parsing resx files...");

var neutral = ParseResx(Path.Combine(localesDir, "Lang.resx"));
var localized = new Dictionary<string, Dictionary<string, ResxEntry>>();
foreach (var c in cultures)
{
    var path = Path.Combine(localesDir, $"Lang.{c}.resx");
    localized[c] = File.Exists(path) ? ParseResx(path) : new();
}

Console.WriteLine($"  Neutral keys: {neutral.Count}");
foreach (var c in cultures)
    Console.WriteLine($"  {c} keys: {localized[c].Count}");

// ---- Phase B: Scan references -----------------------------------------------

Console.WriteLine("Phase B: Scanning references...");

var sourceRoots = new[] { mainProject, smartBpModuleDir, productTourDir };
var xamlRefs = new Dictionary<string, List<string>>(StringComparer.Ordinal); // key -> files
var csharpRefs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
var dynamicXamlRefs = new List<(string file, string expr)>();
var dynamicCsharpRefs = new List<(string file, string expr)>();

ScanReferences(sourceRoots, xamlRefs, csharpRefs, dynamicXamlRefs, dynamicCsharpRefs);

Console.WriteLine($"  XAML literal keys referenced: {xamlRefs.Count}");
Console.WriteLine($"  C# literal keys referenced: {csharpRefs.Count}");
Console.WriteLine($"  Dynamic XAML patterns: {dynamicXamlRefs.Count}");
Console.WriteLine($"  Dynamic C# patterns: {dynamicCsharpRefs.Count}");

// ---- Phase C: Classify keys -------------------------------------------------

Console.WriteLine("Phase C: Classifying keys...");

var keyMap = ClassifyKeys(neutral.Keys, xamlRefs, csharpRefs, repoRoot);

var dictCounts = keyMap.Values.GroupBy(k => k.Dictionary).OrderBy(g => g.Key);
foreach (var g in dictCounts)
    Console.WriteLine($"  {g.Key}: {g.Count()} keys");

// ---- Phase D: Generate split files ------------------------------------------

Console.WriteLine("Phase D: Generating split resource families...");

GenerateSplitFamilies(localesDir, neutral, localized, cultures, keyMap);

// Generate SmartBp module resources
var smartBpLocalesDir = Path.Combine(smartBpModuleDir, "Locales");
GenerateModuleSplitFamilies(smartBpLocalesDir, "SmartBp", neutral, localized, cultures, keyMap);

// ---- Phase E: Write artifacts -----------------------------------------------

Console.WriteLine("Phase E: Writing artifacts...");

WriteInventory(artifactsDir, neutral, localized, cultures, xamlRefs, csharpRefs,
    dynamicXamlRefs, dynamicCsharpRefs, keyMap);
WriteKeyMap(artifactsDir, keyMap, xamlRefs, csharpRefs);
WriteAmbiguousDecisions(artifactsDir, keyMap);
WriteCoverage(artifactsDir, localesDir, keyMap, cultures);

Console.WriteLine("Done. Review artifacts/i18n-migration/");

// ---- Parsing ----------------------------------------------------------------

static Dictionary<string, ResxEntry> ParseResx(string path)
{
    var result = new Dictionary<string, ResxEntry>(StringComparer.Ordinal);
    var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
    foreach (var data in doc.Root!.Elements("data"))
    {
        var name = data.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(name))
            continue;
        // Skip non-string template entries (Name1, Color1, Bitmap1, Icon1)
        var typeAttr = data.Attribute("type");
        if (typeAttr != null)
            continue;
        var value = data.Element("value")?.Value ?? "";
        var comment = data.Element("comment")?.Value ?? "";
        var preserve = data.Attribute(XNamespace.Xml + "space")?.Value == "preserve";
        result[name!] = new ResxEntry(name!, value, comment, preserve);
    }
    return result;
}

// ---- Reference scanning -----------------------------------------------------

static void ScanReferences(
    string[] sourceRoots,
    Dictionary<string, List<string>> xamlRefs,
    Dictionary<string, List<string>> csharpRefs,
    List<(string, string)> dynamicXamlRefs,
    List<(string, string)> dynamicCsharpRefs)
{
    // XAML: {lex:Loc KeyName}  and  {lex:Loc {Binding ...}}
    var lexLocLiteral = new Regex(@"\{lex:Loc\s+([A-Za-z_][A-Za-z0-9_]*)\s*\}");
    var lexLocDynamic = new Regex(@"\{lex:Loc\s+\{Binding\s+([^}]+)\}\s*\}");

    // C#: I18nHelper.GetLocalizedString("KeyName")  or  GetLocalizedString("KeyName")
    // Also GetLocalizedString("prefix" + ...) dynamic patterns
    var csharpLiteral = new Regex(@"GetLocalizedString\(\s*@?""([A-Za-z_][A-Za-z0-9_.]*)""\s*\)");
    var csharpDynamic = new Regex(@"GetLocalizedString\(\s*([A-Za-z_][A-Za-z0-9_.]*\s*\+|[^"")]+\+)");

    foreach (var root in sourceRoots)
    {
        if (!Directory.Exists(root)) continue;
        var files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "build" + Path.DirectorySeparatorChar))
            .ToArray();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file);
            if (ext != ".xaml" && ext != ".cs") continue;
            var content = File.ReadAllText(file);

            if (ext == ".xaml")
            {
                foreach (Match m in lexLocLiteral.Matches(content))
                {
                    var key = m.Groups[1].Value;
                    AddRef(xamlRefs, key, file);
                }
                foreach (Match m in lexLocDynamic.Matches(content))
                {
                    dynamicXamlRefs.Add((file, m.Value));
                }
            }
            else
            {
                foreach (Match m in csharpLiteral.Matches(content))
                {
                    var key = m.Groups[1].Value;
                    // Filter out things that are clearly not resource keys (e.g. "neo-bpsys-wpf")
                    if (key.Contains('.') && !IsValidKey(key)) continue;
                    AddRef(csharpRefs, key, file);
                }
                foreach (Match m in csharpDynamic.Matches(content))
                {
                    dynamicCsharpRefs.Add((file, m.Value));
                }
            }
        }
    }
}

static bool IsValidKey(string key)
{
    // Resource keys may contain dots (e.g. "Designer.Property.X") but not be paths
    return !key.Contains('/') && !key.Contains('\\') && !key.Contains(' ');
}

static void AddRef(Dictionary<string, List<string>> dict, string key, string file)
{
    if (!dict.TryGetValue(key, out var list))
    {
        list = new();
        dict[key] = list;
    }
    if (!list.Contains(file))
        list.Add(file);
}

// ---- Classification ---------------------------------------------------------

static Dictionary<string, KeyClassification> ClassifyKeys(
    IEnumerable<string> keys,
    Dictionary<string, List<string>> xamlRefs,
    Dictionary<string, List<string>> csharpRefs,
    string repoRoot)
{
    var result = new Dictionary<string, KeyClassification>(StringComparer.Ordinal);
    var mainProject = Path.Combine(repoRoot, "neo-bpsys-wpf") + Path.DirectorySeparatorChar;
    var smartBpModule = Path.Combine(repoRoot, "neo-bpsys-wpf.SmartBp.Module") + Path.DirectorySeparatorChar;

    // Genuinely common command/status words
    var commonWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "Confirm", "Cancel", "Save", "Delete", "Close", "Retry", "Loading",
        "Yes", "No", "Apply", "Reset", "Unknown", "OK", "Edit", "Add", "Remove",
        "Start", "Stop", "Refresh", "Preview", "Clear", "Search", "Import", "Export",
        "Copy", "Paste", "Cut", "Undo", "Redo", "Select", "All", "None", "Name",
        "Description", "Status", "Enable", "Disable", "Previous", "Next", "Finish",
        "Continue", "Back", "Update", "Install", "Uninstall", "Open", "Browse",
        "Error", "Warning", "Success", "Info", "Black", "White", "Distance",
        "Author", "Version", "ContributorList", "Sponsor", "About"
    };

    foreach (var key in keys)
    {
        var allFiles = new List<string>();
        if (xamlRefs.TryGetValue(key, out var x)) allFiles.AddRange(x);
        if (csharpRefs.TryGetValue(key, out var c)) allFiles.AddRange(c);

        var (dict, reason) = ClassifyOne(key, allFiles, mainProject, smartBpModule, commonWords);
        result[key] = new KeyClassification
        {
            Key = key,
            Dictionary = dict,
            Assembly = dict == "SmartBp" ? "neo-bpsys-wpf.SmartBp.Module" : "neo-bpsys-wpf",
            Reason = reason,
            IsDynamic = false
        };
    }

    return result;
}

static (string dict, string reason) ClassifyOne(
    string key,
    List<string> files,
    string mainProject,
    string smartBpModule,
    HashSet<string> commonWords)
{
    // 1. Assembly ownership: SmartBp module
    var inModule = files.Any(f => f.StartsWith(smartBpModule, StringComparison.OrdinalIgnoreCase));
    var inHost = files.Any(f => f.StartsWith(mainProject, StringComparison.OrdinalIgnoreCase));

    if (inModule && !inHost && key.StartsWith("SmartBp", StringComparison.OrdinalIgnoreCase))
        return ("SmartBp", "assembly ownership: used only in SmartBp.Module assembly");

    // 2. Prefix-based rules (strong signal)
    var prefix = GetPrefix(key);
    switch (prefix)
    {
        case "Designer":
            if (key.StartsWith("Designer.Animation", StringComparison.OrdinalIgnoreCase))
                return ("AnimationEditor", "prefix Designer.Animation* -> AnimationEditor");
            return ("Designer", "prefix Designer.* -> Designer");
        case "FrontManage":
        case "LayoutPackage":
        case "LegacyConvert":
        case "UserLayout":
        case "BuiltInLayoutScheme":
            return ("FrontManage", $"prefix {prefix}* -> FrontManage");
        case "Score":
            return ("Score", "prefix Score* -> Score");
        case "Team":
        case "Player":
            return ("Team", $"prefix {prefix}* -> Team");
        case "MapBP":
        case "Pick":
        case "Ban":
        case "Talent":
        case "Trait":
            return ("Bp", $"prefix {prefix}* -> Bp");
        case "GameProgress":
        case "NewGame":
        case "GameRule":
            return ("Game", $"prefix {prefix}* -> Game");
        case "Settings":
        case "Update":
            return ("Settings", $"prefix {prefix}* -> Settings");
        case "Plugin":
            return ("PluginMarket", "prefix Plugin* -> PluginMarket");
        case "Tutorial":
            return ("Tutorial", "prefix Tutorial* -> Tutorial");
        case "SmartBp":
            // SmartBp keys used in host -> stay in host under a SmartBp-related dict?
            // The harness says module-owned strings go to the module. But host SmartBP
            // install/missing-module UI stays in host. Use reference location.
            if (inModule && !inHost)
                return ("SmartBp", "SmartBp* used only in module");
            // Host-side SmartBP UI -> keep in host. Which host dict? There's no SmartBp
            // host dict in the target set. These are settings/config -> Settings or Shell.
            // SmartBp host UI is mostly in SettingPage and SmartBpPage.
            break;
    }

    // 3. Reference-location-based classification
    if (files.Count > 0)
    {
        var domains = files.Select(f => MapFileToDomain(f, mainProject, smartBpModule))
            .Where(d => d != null).Distinct().ToList()!;

        if (domains.Count == 1)
        {
            return (domains[0]!, $"single-domain usage: {domains[0]}");
        }

        if (domains.Count >= 3 && commonWords.Contains(key))
        {
            return ("Common", $"cross-domain common word ({domains.Count} domains)");
        }

        if (domains.Count == 2 && commonWords.Contains(key))
        {
            return ("Common", $"cross-domain common word ({domains.Count} domains)");
        }
    }

    // 4. Common words with no references or ambiguous
    if (commonWords.Contains(key))
        return ("Common", "common command/status word");

    // 5. SmartBp host-side keys
    if (key.StartsWith("SmartBp", StringComparison.OrdinalIgnoreCase))
    {
        // Host SmartBP settings/config UI -> Settings (most SmartBp host UI is in settings)
        if (inHost && !inModule)
            return ("Settings", "SmartBp* host settings/config UI");
        return ("Settings", "SmartBp* fallback to Settings");
    }

    // 6. Unreferenced / unclassifiable -> Shell (app shell, navigation, generic)
    return ("Shell", files.Count == 0 ? "unreferenced key -> Shell default" : "ambiguous -> Shell default");
}

static string? GetPrefix(string key)
{
    var dot = key.IndexOf('.');
    if (dot < 0)
    {
        // No dot prefix - check word-prefix heuristics
        if (key.StartsWith("MapBP", StringComparison.OrdinalIgnoreCase)) return "MapBP";
        if (key.StartsWith("Score", StringComparison.OrdinalIgnoreCase)) return "Score";
        if (key.StartsWith("Team", StringComparison.OrdinalIgnoreCase)) return "Team";
        if (key.StartsWith("Player", StringComparison.OrdinalIgnoreCase)) return "Player";
        if (key.StartsWith("Pick", StringComparison.OrdinalIgnoreCase)) return "Pick";
        if (key.StartsWith("Ban", StringComparison.OrdinalIgnoreCase)) return "Ban";
        if (key.StartsWith("Talent", StringComparison.OrdinalIgnoreCase)) return "Talent";
        if (key.StartsWith("Trait", StringComparison.OrdinalIgnoreCase)) return "Trait";
        if (key.StartsWith("GameProgress", StringComparison.OrdinalIgnoreCase)) return "GameProgress";
        if (key.StartsWith("NewGame", StringComparison.OrdinalIgnoreCase)) return "NewGame";
        if (key.StartsWith("GameRule", StringComparison.OrdinalIgnoreCase)) return "GameRule";
        if (key.StartsWith("Settings", StringComparison.OrdinalIgnoreCase)) return "Settings";
        if (key.StartsWith("Update", StringComparison.OrdinalIgnoreCase)) return "Update";
        if (key.StartsWith("Plugin", StringComparison.OrdinalIgnoreCase)) return "Plugin";
        if (key.StartsWith("Tutorial", StringComparison.OrdinalIgnoreCase)) return "Tutorial";
        if (key.StartsWith("SmartBp", StringComparison.OrdinalIgnoreCase)) return "SmartBp";
        if (key.StartsWith("FrontManage", StringComparison.OrdinalIgnoreCase)) return "FrontManage";
        if (key.StartsWith("LayoutPackage", StringComparison.OrdinalIgnoreCase)) return "LayoutPackage";
        if (key.StartsWith("LegacyConvert", StringComparison.OrdinalIgnoreCase)) return "LegacyConvert";
        if (key.StartsWith("UserLayout", StringComparison.OrdinalIgnoreCase)) return "UserLayout";
        if (key.StartsWith("BuiltInLayoutScheme", StringComparison.OrdinalIgnoreCase)) return "BuiltInLayoutScheme";
        return null;
    }
    var p = key[..dot];
    // Only treat known prefixes
    return p switch
    {
        "Designer" => "Designer",
        "FrontManage" => "FrontManage",
        "LayoutPackage" => "LayoutPackage",
        "LegacyConvert" => "LegacyConvert",
        "Score" => "Score",
        "Team" => "Team",
        "Player" => "Player",
        "MapBP" => "MapBP",
        "Pick" => "Pick",
        "Ban" => "Ban",
        "Talent" => "Talent",
        "Trait" => "Trait",
        "GameProgress" => "GameProgress",
        "NewGame" => "NewGame",
        "GameRule" => "GameRule",
        "Settings" => "Settings",
        "Update" => "Update",
        "Plugin" => "Plugin",
        "PluginMarket" => "Plugin",
        "Tutorial" => "Tutorial",
        "SmartBp" => "SmartBp",
        _ => null
    };
}

static string? MapFileToDomain(string file, string mainProject, string smartBpModule)
{
    var f = file.Replace('\\', '/');

    if (f.StartsWith(smartBpModule.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
        return "SmartBp";

    // Normalize relative to main project
    var rel = f;
    var mp = mainProject.Replace('\\', '/');
    if (rel.StartsWith(mp, StringComparison.OrdinalIgnoreCase))
        rel = rel[mp.Length..];

    // Designer + animation
    if (rel.Contains("FrontedBehaviorAnimation", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("GraphEditor", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("AnimationPart", StringComparison.OrdinalIgnoreCase))
        return "AnimationEditor";
    if (rel.Contains("FrontedDesigner", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("Designer", StringComparison.OrdinalIgnoreCase))
        return "Designer";

    // FrontManage
    if (rel.Contains("FrontManage", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("FrontedLayoutPackage", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("FrontedPackageFont", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("LayoutPackage", StringComparison.OrdinalIgnoreCase))
        return "FrontManage";

    // Score
    if (rel.Contains("Score", StringComparison.OrdinalIgnoreCase))
        return "Score";

    // Team
    if (rel.Contains("TeamInfo", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("/Team", StringComparison.OrdinalIgnoreCase))
        return "Team";

    // Game
    if (rel.Contains("GameData", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("GameProgress", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("GameGuidance", StringComparison.OrdinalIgnoreCase))
        return "Game";

    // BP
    if (rel.Contains("MapBp", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("PickPage", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("BanHun", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("BanSur", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("Talent", StringComparison.OrdinalIgnoreCase))
        return "Bp";

    // Settings
    if (rel.Contains("SettingPage", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("SettingPageViewModel", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("UpdaterService", StringComparison.OrdinalIgnoreCase))
        return "Settings";

    // PluginMarket
    if (rel.Contains("Plugin", StringComparison.OrdinalIgnoreCase))
        return "PluginMarket";

    // Tutorial
    if (rel.Contains("Tutorial", StringComparison.OrdinalIgnoreCase))
        return "Tutorial";

    // SmartBp (host-side)
    if (rel.Contains("SmartBp", StringComparison.OrdinalIgnoreCase))
        return "Settings"; // host SmartBP config UI is mostly in settings

    // Shell / navigation
    if (rel.Contains("MainWindow", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("App.xaml", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("HomePage", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("ClassicBackend", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("ClassicPageHost", StringComparison.OrdinalIgnoreCase)
        || rel.Contains("Navigation", StringComparison.OrdinalIgnoreCase))
        return "Shell";

    return null;
}

// ---- Generation -------------------------------------------------------------

static void GenerateSplitFamilies(
    string localesDir,
    Dictionary<string, ResxEntry> neutral,
    Dictionary<string, Dictionary<string, ResxEntry>> localized,
    string[] cultures,
    Dictionary<string, KeyClassification> keyMap)
{
    // Group keys by target dictionary
    var byDict = keyMap.Values
        .Where(k => k.Assembly == "neo-bpsys-wpf") // only host dicts here
        .GroupBy(k => k.Dictionary)
        .ToDictionary(g => g.Key, g => g.Select(k => k.Key).ToList(), StringComparer.Ordinal);

    foreach (var (dictName, dictKeys) in byDict)
    {
        if (dictKeys.Count == 0) continue;

        // Neutral file
        var neutralEntries = dictKeys
            .Where(k => neutral.ContainsKey(k))
            .Select(k => neutral[k])
            .ToList();
        WriteResx(Path.Combine(localesDir, $"{dictName}.resx"), neutralEntries);

        // Culture files
        foreach (var c in cultures)
        {
            var loc = localized[c];
            var entries = dictKeys
                .Where(k => loc.ContainsKey(k))
                .Select(k => loc[k])
                .ToList();
            WriteResx(Path.Combine(localesDir, $"{dictName}.{c}.resx"), entries);
        }

        Console.WriteLine($"  Generated {dictName}: {neutralEntries.Count} neutral keys");
    }
}

static void GenerateModuleSplitFamilies(
    string localesDir,
    string dictName,
    Dictionary<string, ResxEntry> neutral,
    Dictionary<string, Dictionary<string, ResxEntry>> localized,
    string[] cultures,
    Dictionary<string, KeyClassification> keyMap)
{
    Directory.CreateDirectory(localesDir);
    var moduleKeys = keyMap.Values
        .Where(k => k.Assembly != "neo-bpsys-wpf" && k.Dictionary == dictName)
        .Select(k => k.Key)
        .ToList();
    if (moduleKeys.Count == 0) return;

    var neutralEntries = moduleKeys
        .Where(k => neutral.ContainsKey(k))
        .Select(k => neutral[k])
        .ToList();
    WriteResx(Path.Combine(localesDir, $"{dictName}.resx"), neutralEntries);

    foreach (var c in cultures)
    {
        var loc = localized[c];
        var entries = moduleKeys
            .Where(k => loc.ContainsKey(k))
            .Select(k => loc[k])
            .ToList();
        WriteResx(Path.Combine(localesDir, $"{dictName}.{c}.resx"), entries);
    }

    Console.WriteLine($"  Generated module {dictName}: {neutralEntries.Count} neutral keys");
}

static void WriteResx(string path, List<ResxEntry> entries)
{
    var settings = new XmlWriterSettings
    {
        Encoding = new UTF8Encoding(false),
        Indent = true,
        IndentChars = "  ",
        NewLineChars = "\r\n"
    };
    using var writer = XmlWriter.Create(path, settings);
    writer.WriteStartElement("root");
    WriteResxHeader(writer);
    foreach (var e in entries)
    {
        writer.WriteStartElement("data");
        writer.WriteAttributeString("name", e.Key);
        if (e.PreserveSpace)
            writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
        writer.WriteElementString("value", e.Value);
        if (!string.IsNullOrEmpty(e.Comment))
            writer.WriteElementString("comment", e.Comment);
        writer.WriteEndElement();
    }
    writer.WriteEndElement();
}

static void WriteResxHeader(XmlWriter writer)
{
    var headers = new[]
    {
        ("resmimetype", "text/microsoft-resx"),
        ("version", "2.0"),
        ("reader", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
        ("writer", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
    };
    foreach (var (name, value) in headers)
    {
        writer.WriteStartElement("resheader");
        writer.WriteAttributeString("name", name);
        writer.WriteValue(value);
        writer.WriteEndElement();
    }
}

// ---- Artifact writers -------------------------------------------------------

static void WriteInventory(
    string dir,
    Dictionary<string, ResxEntry> neutral,
    Dictionary<string, Dictionary<string, ResxEntry>> localized,
    string[] cultures,
    Dictionary<string, List<string>> xamlRefs,
    Dictionary<string, List<string>> csharpRefs,
    List<(string, string)> dynamicXaml,
    List<(string, string)> dynamicCsharp,
    Dictionary<string, KeyClassification> keyMap)
{
    var sb = new StringBuilder();
    sb.AppendLine("# i18n Migration Inventory");
    sb.AppendLine();
    sb.AppendLine("## Resource files");
    sb.AppendLine();
    sb.AppendLine("| File | Entries |");
    sb.AppendLine("| --- | ---: |");
    sb.AppendLine($"| Locales/Lang.resx (neutral) | {neutral.Count} |");
    foreach (var c in cultures)
        sb.AppendLine($"| Locales/Lang.{c}.resx | {localized[c].Count} |");
    sb.AppendLine("| Locales/Lang.Designer.cs | generated (PublicResXFileCodeGenerator) |");
    sb.AppendLine();

    sb.AppendLine("## Culture suffix convention");
    sb.AppendLine();
    sb.AppendLine("Lowercase: `en-us`, `ja-jp`");
    sb.AppendLine();

    sb.AppendLine("## Translation integrity");
    sb.AppendLine();
    sb.AppendLine("| Culture | Neutral keys | Localized keys | Missing | Localized-only |");
    sb.AppendLine("| --- | ---: | ---: | ---: | ---: |");
    foreach (var c in cultures)
    {
        var loc = localized[c];
        var missing = neutral.Keys.Except(loc.Keys).Count();
        var locOnly = loc.Keys.Except(neutral.Keys).Count();
        sb.AppendLine($"| {c} | {neutral.Count} | {loc.Count} | {missing} | {locOnly} |");
    }
    sb.AppendLine();

    sb.AppendLine("## XAML localization references");
    sb.AppendLine();
    sb.AppendLine($"- Literal `lex:Loc` keys referenced: {xamlRefs.Count}");
    sb.AppendLine($"- Dynamic/binding `lex:Loc` patterns: {dynamicXaml.Count}");
    if (dynamicXaml.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine("### Dynamic XAML patterns");
        foreach (var (file, expr) in dynamicXaml)
            sb.AppendLine($"- `{Path.GetFileName(file)}`: `{expr}`");
    }
    sb.AppendLine();

    sb.AppendLine("## C# localization references");
    sb.AppendLine();
    sb.AppendLine($"- Literal `GetLocalizedString` keys: {csharpRefs.Count}");
    sb.AppendLine($"- Dynamic `GetLocalizedString` patterns: {dynamicCsharp.Count}");
    if (dynamicCsharp.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine("### Dynamic C# patterns");
        foreach (var (file, expr) in dynamicCsharp.Take(50))
            sb.AppendLine($"- `{Path.GetFileName(file)}`: `{expr}`");
    }
    sb.AppendLine();

    sb.AppendLine("## Generated Lang class usage");
    sb.AppendLine();
    sb.AppendLine("- `I18nHelper.GetLocalizedString(key, culture)` uses `Lang.ResourceManager`");
    sb.AppendLine("- `ScorePageViewModel` uses `Lang.ResourceManager`");
    sb.AppendLine();

    sb.AppendLine("## Assembly ownership");
    sb.AppendLine();
    sb.AppendLine("| Assembly | Owns localized UI | Depends on host Lang |");
    sb.AppendLine("| --- | --- | --- |");
    sb.AppendLine("| neo-bpsys-wpf | yes (all current strings) | n/a |");
    sb.AppendLine("| neo-bpsys-wpf.ProductTour | no (no resx, no WPFLocalizeExtension) | yes (via host if any) |");
    sb.AppendLine("| neo-bpsys-wpf.SmartBp.Module | no (no resx) | yes (uses host DefaultDictionary) |");
    sb.AppendLine();

    sb.AppendLine("## Classification summary");
    sb.AppendLine();
    sb.AppendLine("| Dictionary | Keys |");
    sb.AppendLine("| --- | ---: |");
    foreach (var g in keyMap.Values.GroupBy(k => k.Dictionary).OrderBy(g => g.Key))
        sb.AppendLine($"| {g.Key} | {g.Count()} |");
    sb.AppendLine();

    File.WriteAllText(Path.Combine(dir, "inventory.md"), sb.ToString());
}

static void WriteKeyMap(
    string dir,
    Dictionary<string, KeyClassification> keyMap,
    Dictionary<string, List<string>> xamlRefs,
    Dictionary<string, List<string>> csharpRefs)
{
    var sb = new StringBuilder();
    sb.AppendLine("Key,SourceDictionary,TargetAssembly,TargetDictionary,ReferenceCount,ReferenceDomains,MappingReason,IsDynamic");
    foreach (var k in keyMap.Values.OrderBy(k => k.Dictionary).ThenBy(k => k.Key))
    {
        var refCount = (xamlRefs.TryGetValue(k.Key, out var x) ? x.Count : 0)
                     + (csharpRefs.TryGetValue(k.Key, out var c) ? c.Count : 0);
        var domains = string.Join(";", new[] { x ?? new(), c ?? new() }
            .SelectMany(l => l)
            .Select(f => Path.GetFileName(f))
            .Distinct());
        sb.AppendLine($"{k.Key},Locales.Lang,{k.Assembly},Locales.{k.Dictionary},{refCount},\"{domains}\",\"{k.Reason}\",{(k.IsDynamic ? "true" : "false")}");
    }
    File.WriteAllText(Path.Combine(dir, "key-map.csv"), sb.ToString());
}

static void WriteAmbiguousDecisions(string dir, Dictionary<string, KeyClassification> keyMap)
{
    var ambiguous = keyMap.Values
        .Where(k => k.Reason.Contains("ambiguous") || k.Reason.Contains("fallback"))
        .ToList();
    var sb = new StringBuilder();
    sb.AppendLine("# Ambiguous Key Decisions");
    sb.AppendLine();
    if (ambiguous.Count == 0)
    {
        sb.AppendLine("No ambiguous keys. All keys were classified by prefix or single-domain usage.");
    }
    else
    {
        sb.AppendLine("| Key | Target Dictionary | Reason |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var k in ambiguous)
            sb.AppendLine($"| {k.Key} | Locales.{k.Dictionary} | {k.Reason} |");
    }
    File.WriteAllText(Path.Combine(dir, "ambiguous-key-decisions.md"), sb.ToString());
}

static void WriteCoverage(
    string dir,
    string localesDir,
    Dictionary<string, KeyClassification> keyMap,
    string[] cultures)
{
    var byDict = keyMap.Values
        .GroupBy(k => k.Dictionary)
        .ToDictionary(g => g.Key, g => g.Select(k => k.Key).ToHashSet(), StringComparer.Ordinal);

    var sbMd = new StringBuilder();
    sbMd.AppendLine("# Coverage Report");
    sbMd.AppendLine();
    sbMd.AppendLine("| Dictionary | Culture | Neutral keys | Localized keys | Missing | Coverage % | Localized-only |");
    sbMd.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: |");

    var coverageData = new List<object>();

    foreach (var (dictName, dictKeys) in byDict.OrderBy(d => d.Key))
    {
        var neutralPath = Path.Combine(localesDir, $"{dictName}.resx");
        var neutralKeys = File.Exists(neutralPath) ? ParseResx(neutralPath) : new();
        if (neutralKeys.Count == 0) continue;

        sbMd.AppendLine($"| {dictName} | neutral | {neutralKeys.Count} | - | - | - | - |");
        coverageData.Add(new { dictionary = dictName, culture = "neutral", neutralKeys = neutralKeys.Count, localizedKeys = 0, missing = 0, coverage = 100.0, localizedOnly = 0 });

        foreach (var c in cultures)
        {
            var locPath = Path.Combine(localesDir, $"{dictName}.{c}.resx");
            var locKeys = File.Exists(locPath) ? ParseResx(locPath) : new();
            var missing = neutralKeys.Keys.Except(locKeys.Keys).Count();
            var locOnly = locKeys.Keys.Except(neutralKeys.Keys).Count();
            var coverage = neutralKeys.Count > 0
                ? Math.Round((double)locKeys.Count / neutralKeys.Count * 100, 1)
                : 0;
            sbMd.AppendLine($"| {dictName} | {c} | {neutralKeys.Count} | {locKeys.Count} | {missing} | {coverage}% | {locOnly} |");
            coverageData.Add(new { dictionary = dictName, culture = c, neutralKeys = neutralKeys.Count, localizedKeys = locKeys.Count, missing, coverage, localizedOnly = locOnly });
        }
    }

    File.WriteAllText(Path.Combine(dir, "coverage.md"), sbMd.ToString());

    // JSON
    var sbJson = new StringBuilder();
    sbJson.AppendLine("[");
    for (var i = 0; i < coverageData.Count; i++)
    {
        var d = coverageData[i];
        var type = d.GetType();
        var dict = type.GetProperty("dictionary")!.GetValue(d);
        var cult = type.GetProperty("culture")!.GetValue(d);
        var nk = type.GetProperty("neutralKeys")!.GetValue(d);
        var lk = type.GetProperty("localizedKeys")!.GetValue(d);
        var miss = type.GetProperty("missing")!.GetValue(d);
        var cov = type.GetProperty("coverage")!.GetValue(d);
        var lo = type.GetProperty("localizedOnly")!.GetValue(d);
        sbJson.AppendLine($"  {{\"dictionary\":\"{dict}\",\"culture\":\"{cult}\",\"neutralKeys\":{nk},\"localizedKeys\":{lk},\"missing\":{miss},\"coverage\":{cov},\"localizedOnly\":{lo}}}{(i < coverageData.Count - 1 ? "," : "")}");
    }
    sbJson.AppendLine("]");
    File.WriteAllText(Path.Combine(dir, "coverage.json"), sbJson.ToString());
}
