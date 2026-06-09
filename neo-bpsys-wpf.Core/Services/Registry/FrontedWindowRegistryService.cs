using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.Registry;

public sealed class FrontedWindowRegistryService : IFrontedWindowRegistry
{
    internal static List<FrontedWindowInfo> RegisteredWindow { get; } = [];

    private readonly IReadOnlyList<FrontedBuiltInWindowDescriptor> _builtInWindows;
    private readonly IReadOnlyList<FrontedPluginWindowDescriptor> _pluginWindows;
    private readonly IReadOnlyList<IFrontedWindowDescriptor> _windows;
    private readonly Dictionary<string, IFrontedWindowDescriptor> _byWindowId;
    private readonly Dictionary<string, IFrontedWindowDescriptor> _byFullWindowType;

    public FrontedWindowRegistryService()
        : this([], null, null)
    {
    }

    public FrontedWindowRegistryService(
        IEnumerable<IFrontedWindowPluginContributor> pluginContributors,
        IFrontedPluginMetadataProvider? pluginMetadataProvider = null,
        ILogger<FrontedWindowRegistryService>? logger = null)
    {
        logger ??= NullLogger<FrontedWindowRegistryService>.Instance;
        _builtInWindows = GetBuiltInV3Windows()
            .Concat(RegisteredWindow
                .Where(info => !IsBuiltInV3Window(info.Name)
                               && !string.Equals(info.Name, "WidgetsWindow", StringComparison.Ordinal))
                .Select(FrontedBuiltInWindowDescriptor.FromInfo))
            .ToArray();

        var acceptedPluginWindows = new List<FrontedPluginWindowDescriptor>();
        foreach (var contributor in pluginContributors)
        {
            IReadOnlyList<FrontedPluginWindowDescriptor> descriptors;
            try
            {
                descriptors = contributor.GetFrontedWindows().ToArray();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Plugin fronted window contributor {ContributorType} failed.", contributor.GetType().FullName);
                continue;
            }

            foreach (var descriptor in descriptors)
            {
                try
                {
                    if (pluginMetadataProvider?.TryGetPluginFolder(descriptor.PackageId, out var pluginFolder) == true)
                    {
                        descriptor.PluginFolder = pluginFolder;
                    }

                    descriptor.Validate(descriptor.PluginFolder);
                    acceptedPluginWindows.Add(descriptor);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Rejected plugin fronted window descriptor {FullWindowType}.",
                        descriptor.FullWindowType);
                }
            }
        }

        _pluginWindows = acceptedPluginWindows.ToArray();
        var candidates = _builtInWindows.Cast<IFrontedWindowDescriptor>()
            .Concat(_pluginWindows)
            .ToArray();

        _byWindowId = BuildIndex(
            candidates,
            descriptor => descriptor.WindowId,
            "WindowId",
            logger);
        _byFullWindowType = BuildIndex(
            _byWindowId.Values,
            descriptor => descriptor.FullWindowType,
            "FullWindowType",
            logger);

        _windows = _byFullWindowType.Values.ToArray();
        _builtInWindows = _windows.OfType<FrontedBuiltInWindowDescriptor>().ToArray();
        _pluginWindows = _windows.OfType<FrontedPluginWindowDescriptor>().ToArray();
    }

    public IReadOnlyList<IFrontedWindowDescriptor> GetWindows() => _windows;

    public IReadOnlyList<IFrontedWindowDescriptor> GetCustomizableLayoutWindows()
    {
        return _windows
            .Where(descriptor => descriptor.IsV3LayoutWindow && descriptor.Customizable)
            .ToArray();
    }

    public IReadOnlyList<IFrontedWindowDescriptor> GetManageableWindows()
    {
        return _windows
            .Where(descriptor => descriptor.IsVisibleInFrontManage)
            .OrderBy(descriptor => string.IsNullOrWhiteSpace(descriptor.GroupKey)
                ? descriptor.IsPlugin ? "Plugin" : "BuiltIn"
                : descriptor.GroupKey,
                StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.DisplayOrder ?? int.MaxValue)
            .ThenBy(descriptor => string.IsNullOrWhiteSpace(descriptor.DisplayName)
                ? descriptor.WindowTypeName
                : descriptor.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool TryGetByWindowId(string windowId, out IFrontedWindowDescriptor descriptor) =>
        _byWindowId.TryGetValue(windowId, out descriptor!);

    public bool TryGetByFullWindowType(string fullWindowType, out IFrontedWindowDescriptor descriptor) =>
        _byFullWindowType.TryGetValue(fullWindowType, out descriptor!);

    public IReadOnlyList<FrontedPluginWindowDescriptor> GetPluginWindows() => _pluginWindows;

    public IReadOnlyList<FrontedBuiltInWindowDescriptor> GetBuiltInWindows() => _builtInWindows;

    private static IReadOnlyList<FrontedBuiltInWindowDescriptor> GetBuiltInV3Windows()
    {
        return
        [
            CreateBuiltInV3Descriptor(FrontedWindowType.BpWindow, 0),
            CreateBuiltInV3Descriptor(FrontedWindowType.CutSceneWindow, 100),
            CreateBuiltInV3Descriptor(FrontedWindowType.ScoreSurWindow, 300),
            CreateBuiltInV3Descriptor(FrontedWindowType.ScoreHunWindow, 400),
            CreateBuiltInV3Descriptor(FrontedWindowType.ScoreGlobalWindow, 500),
            CreateBuiltInV3Descriptor(FrontedWindowType.GameDataWindow, 600),
            CreateBuiltInV3Descriptor(FrontedWindowType.BpOverviewWindow, 700),
            CreateBuiltInV3Descriptor(FrontedWindowType.MapV2Window, 710)
        ];
    }

    private static FrontedBuiltInWindowDescriptor CreateBuiltInV3Descriptor(
        FrontedWindowType windowType,
        int displayOrder)
    {
        var windowTypeName = windowType.ToString();
        return new FrontedBuiltInWindowDescriptor
        {
            WindowId = FrontedWindowHelper.GetFrontedWindowGuid(windowType),
            WindowTypeName = windowTypeName,
            DisplayName = windowTypeName,
            DisplayNameKey = $"Designer.Window.{windowTypeName}",
            GroupKey = "BuiltIn",
            DisplayOrder = displayOrder,
            IsV3LayoutWindow = true,
            Customizable = true
        };
    }

    private static bool IsBuiltInV3Window(string windowTypeName)
    {
        return Enum.TryParse<FrontedWindowType>(windowTypeName, ignoreCase: false, out var windowType)
               && windowType is not FrontedWindowType.ScoreWindow;
    }

    private static Dictionary<string, IFrontedWindowDescriptor> BuildIndex(
        IEnumerable<IFrontedWindowDescriptor> descriptors,
        Func<IFrontedWindowDescriptor, string> keySelector,
        string keyName,
        ILogger logger)
    {
        var index = new Dictionary<string, IFrontedWindowDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in descriptors)
        {
            var key = keySelector(descriptor);
            if (string.IsNullOrWhiteSpace(key))
            {
                logger.LogWarning("Rejected fronted window descriptor with empty {KeyName}.", keyName);
                continue;
            }

            if (index.ContainsKey(key))
            {
                logger.LogWarning(
                    "Rejected duplicate fronted window descriptor {KeyName}: {Key}.",
                    keyName,
                    key);
                continue;
            }

            index[key] = descriptor;
        }

        return index;
    }
}
