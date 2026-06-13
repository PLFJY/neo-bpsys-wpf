using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Default in-memory app-level behavior clipboard.
/// </summary>
public sealed class FrontedBehaviorClipboard : IFrontedBehaviorClipboard
{
    /// <inheritdoc />
    public FrontedBehaviorClipboardPayload? Payload { get; private set; }

    /// <inheritdoc />
    public void Set(FrontedBehaviorClipboardPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Payload = payload;
    }
}

/// <summary>
/// Conservatively infers semantic control indexes from explicit config, bindings, and names.
/// </summary>
public sealed partial class FrontedBehaviorControlSemanticResolver : IFrontedBehaviorControlSemanticResolver
{
    /// <inheritdoc />
    public FrontedBehaviorControlSemanticInfo Resolve(FrontedControlDesignItem control)
    {
        ArgumentNullException.ThrowIfNull(control);

        var explicitIndex = control.Config is TalentTraitDisplayControlConfig talent
            ? talent.PlayerIndex
            : null;
        var bindingIndexes = EnumerateBindingPaths(control.Config)
            .SelectMany(path => BindingIndexRegex().Matches(path).Select(match => int.Parse(match.Groups[1].Value)))
            .Distinct()
            .ToArray();
        int? bindingIndex = bindingIndexes.Length == 1 ? bindingIndexes[0] : null;
        var nameMatch = TrailingIndexRegex().Match(control.Name);
        int? nameIndex = nameMatch.Success ? int.Parse(nameMatch.Groups[1].Value) : null;

        return new FrontedBehaviorControlSemanticInfo
        {
            Index = explicitIndex ?? bindingIndex ?? nameIndex,
            Role = nameMatch.Success ? control.Name[..nameMatch.Index] : control.Name,
            Group = control.Config.ControlType
        };
    }

    private static IEnumerable<string> EnumerateBindingPaths(FrontedControlConfigBase config)
    {
        if (!string.IsNullOrWhiteSpace(config.BindingPath))
        {
            yield return config.BindingPath;
        }

        if (config is ImageFrontedControlConfig image
            && !string.IsNullOrWhiteSpace(image.LockVisibilityBindingPath))
        {
            yield return image.LockVisibilityBindingPath;
        }
    }

    [GeneratedRegex(@"\[(\d+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex BindingIndexRegex();

    [GeneratedRegex(@"(\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingIndexRegex();
}

/// <summary>
/// Copies, previews, validates, and pastes fronted behaviors between controls.
/// </summary>
public sealed class FrontedBehaviorCopyPasteService
{
    private static readonly HashSet<string> IndexFilterFields =
    [
        "Event.Index",
        "Event.Indexes",
        "Event.IndexesText",
        "Event.PreviousIndex",
        "Event.PreviousIndexes",
        "Event.PreviousIndexesText"
    ];

    private readonly IFrontedBehaviorControlSemanticResolver _semanticResolver;
    private readonly IFrontedDesignerLocalizationService _localizationService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    /// <summary>
    /// Initializes a behavior copy/paste service.
    /// </summary>
    /// <param name="semanticResolver">The semantic resolver used for trigger index remapping.</param>
    /// <param name="localizationService">The localization service used for user-visible paste messages.</param>
    public FrontedBehaviorCopyPasteService(
        IFrontedBehaviorControlSemanticResolver semanticResolver,
        IFrontedDesignerLocalizationService? localizationService = null)
    {
        _semanticResolver = semanticResolver;
        _localizationService = localizationService ?? new FrontedDesignerLocalizationService();
    }

    /// <summary>
    /// Creates a deep-cloned clipboard payload.
    /// </summary>
    /// <param name="windowType">The source window type.</param>
    /// <param name="source">The source control.</param>
    /// <param name="behavior">The source behavior.</param>
    /// <returns>The clipboard payload.</returns>
    public FrontedBehaviorClipboardPayload Copy(string windowType, FrontedControlDesignItem source, FrontedBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(behavior);

        var clone = Clone(behavior);
        return new FrontedBehaviorClipboardPayload
        {
            SourceWindowType = windowType ?? string.Empty,
            SourceControlName = source.Name,
            SourceControlBehaviorGuid = source.Config.BehaviorGuid,
            SourceSemanticIndex = _semanticResolver.Resolve(source).Index,
            Behavior = clone,
            Requirements = CollectRequirements(clone, source.Config.BehaviorGuid)
        };
    }

    /// <summary>
    /// Previews compatibility and rewrites for one target control.
    /// </summary>
    /// <param name="payload">The copied behavior payload.</param>
    /// <param name="target">The target control.</param>
    /// <param name="options">The paste options.</param>
    /// <returns>The paste preview.</returns>
    public FrontedBehaviorPastePreview Preview(
        FrontedBehaviorClipboardPayload payload,
        FrontedControlDesignItem target,
        FrontedBehaviorPasteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(target);
        options ??= new FrontedBehaviorPasteOptions();

        var preview = new FrontedBehaviorPastePreview { Target = target };
        ValidateCompatibility(payload, target, preview.CompatibilityErrors);
        preview.IsCompatible = preview.CompatibilityErrors.Count == 0;

        var targetGuid = target.Config.BehaviorGuid;
        foreach (var targetReference in EnumerateTargetReferences(payload.Behavior))
        {
            var parsed = FrontedAnimationTargetReference.Parse(targetReference);
            if (parsed.BehaviorGuid == payload.SourceControlBehaviorGuid)
            {
                if (options.RewriteAnimationTargets && targetGuid != Guid.Empty)
                {
                    preview.TargetRewrites.Add(new FrontedBehaviorPasteRewrite
                    {
                        Before = targetReference,
                        After = RewriteTargetReference(parsed, targetGuid)
                    });
                }
            }
            else if (parsed.Kind is FrontedAnimationTargetReferenceKind.BehaviorGuid
                     or FrontedAnimationTargetReferenceKind.GeneratedPart)
            {
                preview.ExternalReferences.Add(targetReference);
            }
        }

        var targetIndex = _semanticResolver.Resolve(target).Index;
        preview.IsTriggerIndexRemapAvailable =
            payload.SourceSemanticIndex.HasValue && targetIndex.HasValue;
        if (!preview.IsTriggerIndexRemapAvailable)
        {
            preview.TriggerIndexRemapUnavailableReason = payload.SourceSemanticIndex.HasValue
                ? Localize(
                    "Designer.Behaviors.TargetIndexUnavailable",
                    "Target control index cannot be inferred.")
                : Localize(
                    "Designer.Behaviors.SourceIndexUnavailable",
                    "Source control index cannot be inferred.");
        }
        else if (options.RewriteTriggerIndexes)
        {
            foreach (var filter in EnumerateFilters(payload.Behavior))
            {
                if (TryRewriteFilter(filter, payload.SourceSemanticIndex!.Value, targetIndex!.Value, out var rewritten))
                {
                    preview.TriggerRewrites.Add(new FrontedBehaviorPasteRewrite
                    {
                        Before = FormatFilter(filter.Left, filter.Operator, filter.Right),
                        After = FormatFilter(filter.Left, filter.Operator, rewritten)
                    });
                }
            }
        }

        return preview;
    }

    /// <summary>
    /// Pastes a deep-cloned behavior into one target behavior document.
    /// </summary>
    /// <param name="payload">The copied behavior payload.</param>
    /// <param name="target">The target control.</param>
    /// <param name="document">The destination behavior document.</param>
    /// <param name="options">The paste options.</param>
    /// <returns>The paste result.</returns>
    public FrontedBehaviorPasteResult Paste(
        FrontedBehaviorClipboardPayload payload,
        FrontedControlDesignItem target,
        FrontedBehaviorDocument document,
        FrontedBehaviorPasteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new FrontedBehaviorPasteOptions();

        var preview = Preview(payload, target, options);
        if (!preview.IsCompatible)
        {
            return new FrontedBehaviorPasteResult { Preview = preview };
        }

        if (target.Config.BehaviorGuid == Guid.Empty)
        {
            target.Config.BehaviorGuid = FrontedBehaviorGuidHelper.NewGuid();
            preview = Preview(payload, target, options);
        }

        var clone = Clone(payload.Behavior);
        if (options.GenerateNewBehaviorId)
        {
            clone.BehaviorId = FrontedBehaviorGuidHelper.NewGuid();
        }

        RegenerateGraphIds(clone.Graph);
        RegenerateGraphIds(clone.StartGraph);
        RegenerateGraphIds(clone.LoopGraph);
        RegenerateGraphIds(clone.StopGraph);

        if (options.RewriteAnimationTargets)
        {
            RewriteTargets(clone, payload.SourceControlBehaviorGuid, target.Config.BehaviorGuid);
        }

        var targetIndex = _semanticResolver.Resolve(target).Index;
        if (options.RewriteTriggerIndexes && payload.SourceSemanticIndex.HasValue && targetIndex.HasValue)
        {
            RewriteTriggerIndexes(clone, payload.SourceSemanticIndex.Value, targetIndex.Value);
        }

        var set = document.GetOrCreateSet(target.Config.BehaviorGuid, target.Name);
        set.DisplayName = target.Name;
        clone.Name = CreateUniqueName(
            options.KeepBehaviorName ? clone.Name : string.Empty,
            set.Behaviors.Select(item => item.Name));
        set.Behaviors.Add(clone);

        return new FrontedBehaviorPasteResult
        {
            Succeeded = true,
            Preview = preview,
            Behavior = clone
        };
    }

    private void ValidateCompatibility(
        FrontedBehaviorClipboardPayload payload,
        FrontedControlDesignItem target,
        ICollection<string> errors)
    {
        foreach (var requirement in payload.Requirements)
        {
            switch (requirement.Kind)
            {
                case FrontedAnimationPartNames.PickingBorder
                    when target.Config is not ImageFrontedControlConfig { PickingBorderAvailable: true }:
                    errors.Add(Localize(
                        "Designer.Behaviors.TargetMissingPickingBorder",
                        "Target control does not have Picking Border enabled."));
                    break;
                case FrontedAnimationPartNames.LockOverlay
                    when target.Config is not ImageFrontedControlConfig { Lockable: true }:
                    errors.Add(Localize(
                        "Designer.Behaviors.TargetMissingLockOverlay",
                        "Target control does not have Ban Lock / Lock Overlay enabled."));
                    break;
                default:
                    if (!string.Equals(requirement.Kind, FrontedAnimationPartNames.PickingBorder, StringComparison.Ordinal)
                        && !string.Equals(requirement.Kind, FrontedAnimationPartNames.LockOverlay, StringComparison.Ordinal)
                        && !target.Config.PseudoElements.Any(item =>
                            string.Equals(item.Name, requirement.Kind, StringComparison.Ordinal)))
                    {
                        errors.Add(string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            Localize(
                                "Designer.Behaviors.TargetMissingPseudoElement",
                                "Target control does not have pseudo-element '{0}'."),
                            requirement.Kind));
                    }

                    break;
            }
        }
    }

    private static List<FrontedBehaviorCopyRequirement> CollectRequirements(FrontedBehavior behavior, Guid sourceGuid) =>
        EnumerateTargetReferences(behavior)
            .Select(value => (Value: value, Parsed: FrontedAnimationTargetReference.Parse(value)))
            .Where(item => item.Parsed.Kind == FrontedAnimationTargetReferenceKind.GeneratedPart
                           && item.Parsed.BehaviorGuid == sourceGuid)
            .Select(item => new FrontedBehaviorCopyRequirement
            {
                Kind = item.Parsed.PartName ?? string.Empty,
                Source = item.Value
            })
            .DistinctBy(item => item.Kind)
            .ToList();

    private static IEnumerable<FrontedNodeGraph> EnumerateGraphs(FrontedBehavior behavior)
    {
        yield return behavior.Graph;
        yield return behavior.StartGraph;
        yield return behavior.LoopGraph;
        yield return behavior.StopGraph;
    }

    private static IEnumerable<string> EnumerateTargetReferences(FrontedBehavior behavior) =>
        EnumerateGraphs(behavior)
            .SelectMany(graph => graph.Nodes)
            .Where(node => node.Properties.ContainsKey("Target"))
            .Select(node => node.Properties["Target"])
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>();

    private static IEnumerable<TriggerFilter> EnumerateFilters(FrontedBehavior behavior) =>
        new[] { behavior.Trigger, behavior.StartTrigger, behavior.EndTrigger }
            .Where(trigger => trigger is not null)
            .SelectMany(trigger => trigger!.Filters);

    private static string RewriteTargetReference(FrontedAnimationTargetReference reference, Guid targetGuid) =>
        reference.Kind == FrontedAnimationTargetReferenceKind.GeneratedPart
            ? $"part:{targetGuid}:{reference.PartName}"
            : $"guid:{targetGuid}";

    private static void RewriteTargets(FrontedBehavior behavior, Guid sourceGuid, Guid targetGuid)
    {
        foreach (var node in EnumerateGraphs(behavior).SelectMany(graph => graph.Nodes))
        {
            if (!node.Properties.TryGetValue("Target", out var value) || value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var parsed = FrontedAnimationTargetReference.Parse(value.GetString());
            if (parsed.BehaviorGuid == sourceGuid)
            {
                node.Properties["Target"] = JsonSerializer.SerializeToElement(RewriteTargetReference(parsed, targetGuid));
            }
        }
    }

    private static void RewriteTriggerIndexes(FrontedBehavior behavior, int sourceIndex, int targetIndex)
    {
        foreach (var filter in EnumerateFilters(behavior))
        {
            if (TryRewriteFilter(filter, sourceIndex, targetIndex, out var rewritten))
            {
                filter.Right = rewritten;
            }
        }
    }

    private static bool TryRewriteFilter(TriggerFilter filter, int sourceIndex, int targetIndex, out string rewritten)
    {
        rewritten = filter.Right ?? string.Empty;
        if (!IndexFilterFields.Contains(filter.Left) || filter.Right is null)
        {
            return false;
        }

        var source = sourceIndex.ToString();
        if (string.Equals(filter.Right.Trim(), source, StringComparison.Ordinal))
        {
            rewritten = targetIndex.ToString();
            return true;
        }

        var supportsBracketedEquals =
            filter.Operator == TriggerFilterOperator.Equals
            && filter.Left is "Event.IndexesText" or "Event.PreviousIndexesText";
        if (supportsBracketedEquals
            && string.Equals(filter.Right.Trim(), $"[{source}]", StringComparison.Ordinal))
        {
            rewritten = $"[{targetIndex}]";
            return true;
        }

        return false;
    }

    private static string FormatFilter(string left, TriggerFilterOperator @operator, string? right) =>
        $"{left} {@operator} {right}";

    private string CreateUniqueName(string name, IEnumerable<string> existingNames)
    {
        var baseName = string.IsNullOrWhiteSpace(name)
            ? Localize("Designer.Behaviors.DefaultName", "Behavior")
            : name.Trim();
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        var copyName = string.Format(
            Localize("Designer.Behaviors.CopyNameFormat", "{0} Copy"),
            baseName);
        if (!existing.Contains(copyName))
        {
            return copyName;
        }

        for (var index = 2; ; index++)
        {
            var candidate = $"{copyName} {index}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private FrontedBehavior Clone(FrontedBehavior behavior)
    {
        var json = JsonSerializer.Serialize(behavior, _jsonOptions);
        return JsonSerializer.Deserialize<FrontedBehavior>(json, _jsonOptions)
               ?? throw new InvalidOperationException("Unable to clone fronted behavior.");
    }

    private string Localize(string key, string fallback) =>
        _localizationService.GetDesignerText(key, fallback);

    private static void RegenerateGraphIds(FrontedNodeGraph graph)
    {
        var nodeIds = new Dictionary<Guid, Guid>();
        foreach (var node in graph.Nodes)
        {
            var oldId = node.NodeId;
            node.NodeId = FrontedBehaviorGuidHelper.NewGuid();
            nodeIds[oldId] = node.NodeId;
        }

        foreach (var connection in graph.Connections)
        {
            connection.ConnectionId = FrontedBehaviorGuidHelper.NewGuid();
            if (nodeIds.TryGetValue(connection.SourceNodeId, out var sourceNodeId))
            {
                connection.SourceNodeId = sourceNodeId;
            }

            if (nodeIds.TryGetValue(connection.TargetNodeId, out var targetNodeId))
            {
                connection.TargetNodeId = targetNodeId;
            }
        }
    }
}
