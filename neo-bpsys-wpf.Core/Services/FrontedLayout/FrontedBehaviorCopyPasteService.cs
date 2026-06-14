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
    private static readonly HashSet<string> ScalarIndexFilterFields =
    [
        "Event.Index",
        "Event.PlayerIndex",
        "Event.SourceIndex",
        "Event.TargetIndex",
        "Event.PreviousIndex"
    ];

    private static readonly HashSet<string> CollectionIndexFilterFields =
    [
        "Event.Indexes",
        "Event.IndexesText",
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
    public FrontedBehaviorClipboardPayload Copy(
        string windowType,
        FrontedControlDesignItem source,
        FrontedBehavior behavior,
        FrontedBehaviorDocument? sourceDocument = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(behavior);

        var clone = Clone(behavior);
        var requirements = CollectRequirements(clone, source.Config.BehaviorGuid);
        var sourceSet = sourceDocument?.FindSet(source.Config.BehaviorGuid);
        return new FrontedBehaviorClipboardPayload
        {
            SourceWindowType = windowType ?? string.Empty,
            SourceControlName = source.Name,
            SourceControlBehaviorGuid = source.Config.BehaviorGuid,
            SourceSemanticIndex = _semanticResolver.Resolve(source).Index,
            Behavior = clone,
            Requirements = requirements,
            AnimationParts = requirements
                .Where(requirement => !IsBuiltInPartRequirement(requirement.Kind))
                .Select(requirement => sourceSet?.AnimationParts.FirstOrDefault(part =>
                    string.Equals(part.Name, requirement.Kind, StringComparison.Ordinal)))
                .Where(part => part is not null)
                .Select(part => Clone(part!))
                .ToList()
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

            foreach (var filter in EnumerateGraphConditionFilters(payload.Behavior))
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
        RegenerateGraphIds(clone.ExitGraph);
        RegenerateGraphIds(clone.EnterGraph);

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
        EnsureRequiredAnimationParts(payload, set);
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
                    if (!IsBuiltInPartRequirement(requirement.Kind)
                        && !payload.AnimationParts.Any(item =>
                            string.Equals(item.Name, requirement.Kind, StringComparison.Ordinal)))
                    {
                        errors.Add(string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            Localize(
                                "Designer.Behaviors.MissingAnimationPart",
                                "Animation part '{0}' is missing for control {1}."),
                            requirement.Kind,
                            payload.SourceControlName));
                    }

                    break;
            }
        }
    }

    private static void EnsureRequiredAnimationParts(
        FrontedBehaviorClipboardPayload payload,
        ControlBehaviorSet targetSet)
    {
        foreach (var requirement in payload.Requirements.Where(requirement => !IsBuiltInPartRequirement(requirement.Kind)))
        {
            if (targetSet.AnimationParts.Any(part =>
                    string.Equals(part.Name, requirement.Kind, StringComparison.Ordinal)))
            {
                continue;
            }

            var sourcePart = payload.AnimationParts.FirstOrDefault(part =>
                string.Equals(part.Name, requirement.Kind, StringComparison.Ordinal));
            if (sourcePart is not null)
            {
                targetSet.AnimationParts.Add(Clone(sourcePart));
            }
        }
    }

    private static bool IsBuiltInPartRequirement(string kind) =>
        string.Equals(kind, FrontedAnimationPartNames.PickingBorder, StringComparison.Ordinal)
        || string.Equals(kind, FrontedAnimationPartNames.LockOverlay, StringComparison.Ordinal);

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
        yield return behavior.ExitGraph;
        yield return behavior.EnterGraph;
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
        new[] { behavior.Trigger, behavior.StartTrigger, behavior.TransitionTrigger }
            .Concat(behavior.StopTriggers)
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

        foreach (var node in EnumerateGraphs(behavior).SelectMany(graph => graph.Nodes))
        {
            if (!TryCreateGraphConditionFilter(node, out var filter))
            {
                continue;
            }

            if (TryRewriteFilter(filter, sourceIndex, targetIndex, out var rewritten))
            {
                node.Properties["Right"] = JsonSerializer.SerializeToElement(rewritten);
            }
        }
    }

    private static bool TryRewriteFilter(TriggerFilter filter, int sourceIndex, int targetIndex, out string rewritten)
    {
        rewritten = filter.Right ?? string.Empty;
        if (filter.Right is null)
        {
            return false;
        }

        var normalizedLeft = NormalizeEventIndexField(filter.Left);
        if (ScalarIndexFilterFields.Contains(normalizedLeft))
        {
            return TryRewriteScalarIndex(filter.Right, sourceIndex, targetIndex, out rewritten);
        }

        if (CollectionIndexFilterFields.Contains(normalizedLeft))
        {
            return TryRewriteCollectionIndex(filter.Right, sourceIndex, targetIndex, out rewritten);
        }

        return false;
    }

    private static bool TryRewriteScalarIndex(string right, int sourceIndex, int targetIndex, out string rewritten)
    {
        rewritten = right;
        if (string.Equals(right.Trim(), sourceIndex.ToString(), StringComparison.Ordinal))
        {
            rewritten = targetIndex.ToString();
            return true;
        }

        return false;
    }

    private static bool TryRewriteCollectionIndex(string right, int sourceIndex, int targetIndex, out string rewritten)
    {
        if (TryRewriteScalarIndex(right, sourceIndex, targetIndex, out rewritten))
        {
            return true;
        }

        if (TryRewriteBracketedIndexList(right, sourceIndex, targetIndex, out rewritten))
        {
            return true;
        }

        return false;
    }

    private static bool TryRewriteBracketedIndexList(string right, int sourceIndex, int targetIndex, out string rewritten)
    {
        rewritten = right;
        var trimmed = right.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '[' || trimmed[^1] != ']')
        {
            return false;
        }

        var parts = trimmed[1..^1]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var indexes = new List<int>(parts.Length);
        var changed = false;
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var index))
            {
                return false;
            }

            if (index == sourceIndex)
            {
                indexes.Add(targetIndex);
                changed = true;
            }
            else
            {
                indexes.Add(index);
            }
        }

        if (!changed)
        {
            return false;
        }

        rewritten = $"[{string.Join(", ", indexes)}]";
        return true;
    }

    private static string NormalizeEventIndexField(string left)
    {
        if (left.StartsWith("StartEvent.", StringComparison.Ordinal))
        {
            return $"Event.{left["StartEvent.".Length..]}";
        }

        if (left.StartsWith("StopEvent.", StringComparison.Ordinal))
        {
            return $"Event.{left["StopEvent.".Length..]}";
        }

        return left;
    }

    private static IEnumerable<TriggerFilter> EnumerateGraphConditionFilters(FrontedBehavior behavior) =>
        EnumerateGraphs(behavior)
            .SelectMany(graph => graph.Nodes)
            .Select(node => TryCreateGraphConditionFilter(node, out var filter) ? filter : null)
            .Where(filter => filter is not null)
            .Cast<TriggerFilter>();

    private static bool TryCreateGraphConditionFilter(FrontedNode node, out TriggerFilter filter)
    {
        filter = new TriggerFilter();
        if (!string.Equals(node.NodeType, "flow.if", StringComparison.Ordinal)
            || !TryGetStringProperty(node, "Left", out var left)
            || !TryGetStringProperty(node, "Right", out var right))
        {
            return false;
        }

        filter.Left = left;
        filter.Right = right;
        filter.Operator = TryGetStringProperty(node, "Operator", out var operatorText)
            && Enum.TryParse<TriggerFilterOperator>(operatorText, out var parsed)
                ? parsed
                : TriggerFilterOperator.Equals;
        return true;
    }

    private static bool TryGetStringProperty(FrontedNode node, string name, out string value)
    {
        value = string.Empty;
        if (!node.Properties.TryGetValue(name, out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return true;
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

    private static FrontedAnimationPartConfig Clone(FrontedAnimationPartConfig part)
    {
        var json = JsonSerializer.Serialize(part);
        return JsonSerializer.Deserialize<FrontedAnimationPartConfig>(json)
               ?? throw new InvalidOperationException("Unable to clone fronted animation part.");
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
