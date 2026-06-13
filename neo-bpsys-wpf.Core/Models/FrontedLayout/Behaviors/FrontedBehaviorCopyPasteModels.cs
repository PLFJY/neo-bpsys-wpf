using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// App-level clipboard payload for copying one fronted behavior between controls.
/// </summary>
public sealed class FrontedBehaviorClipboardPayload
{
    /// <summary>
    /// Gets or sets the clipboard payload version.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the source window type.
    /// </summary>
    public string SourceWindowType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source control name.
    /// </summary>
    public string SourceControlName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source control behavior GUID.
    /// </summary>
    public Guid SourceControlBehaviorGuid { get; set; }

    /// <summary>
    /// Gets or sets the inferred semantic index of the source control.
    /// </summary>
    public int? SourceSemanticIndex { get; set; }

    /// <summary>
    /// Gets or sets the copied behavior snapshot.
    /// </summary>
    public FrontedBehavior Behavior { get; set; } = new();

    /// <summary>
    /// Gets or sets generated-part and control-kind requirements.
    /// </summary>
    public List<FrontedBehaviorCopyRequirement> Requirements { get; set; } = [];
}

/// <summary>
/// Describes one compatibility requirement discovered while copying a behavior.
/// </summary>
public sealed class FrontedBehaviorCopyRequirement
{
    /// <summary>
    /// Gets or sets the requirement kind.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target reference that caused the requirement.
    /// </summary>
    public string? Source { get; set; }
}

/// <summary>
/// Options applied while pasting a copied behavior.
/// </summary>
public sealed class FrontedBehaviorPasteOptions
{
    /// <summary>
    /// Gets or sets whether source-control animation targets are rewritten.
    /// </summary>
    public bool RewriteAnimationTargets { get; set; } = true;

    /// <summary>
    /// Gets or sets whether supported trigger index filters are rewritten.
    /// </summary>
    public bool RewriteTriggerIndexes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the pasted behavior receives a new behavior identifier.
    /// </summary>
    public bool GenerateNewBehaviorId { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the copied behavior name is retained.
    /// </summary>
    public bool KeepBehaviorName { get; set; } = true;
}

/// <summary>
/// Semantic information inferred from a fronted control.
/// </summary>
public sealed class FrontedBehaviorControlSemanticInfo
{
    /// <summary>
    /// Gets or sets the control-specific semantic index.
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    /// Gets or sets the inferred semantic role.
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Gets or sets the inferred semantic group.
    /// </summary>
    public string? Group { get; set; }
}

/// <summary>
/// Resolves semantic information used when copying behaviors between controls.
/// </summary>
public interface IFrontedBehaviorControlSemanticResolver
{
    /// <summary>
    /// Resolves semantic information for a design control.
    /// </summary>
    /// <param name="control">The design control to inspect.</param>
    /// <returns>The inferred semantic information.</returns>
    FrontedBehaviorControlSemanticInfo Resolve(FrontedControlDesignItem control);
}

/// <summary>
/// Describes one value rewrite shown in a behavior paste preview.
/// </summary>
public sealed class FrontedBehaviorPasteRewrite
{
    /// <summary>
    /// Gets or sets the original value.
    /// </summary>
    public string Before { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rewritten value.
    /// </summary>
    public string After { get; set; } = string.Empty;
}

/// <summary>
/// Describes compatibility and rewrites for one behavior paste target.
/// </summary>
public sealed class FrontedBehaviorPastePreview
{
    /// <summary>
    /// Gets or sets the target control.
    /// </summary>
    public required FrontedControlDesignItem Target { get; set; }

    /// <summary>
    /// Gets or sets whether the target is compatible.
    /// </summary>
    public bool IsCompatible { get; set; }

    /// <summary>
    /// Gets or sets compatibility errors.
    /// </summary>
    public List<string> CompatibilityErrors { get; set; } = [];

    /// <summary>
    /// Gets or sets animation target rewrites.
    /// </summary>
    public List<FrontedBehaviorPasteRewrite> TargetRewrites { get; set; } = [];

    /// <summary>
    /// Gets or sets trigger filter rewrites.
    /// </summary>
    public List<FrontedBehaviorPasteRewrite> TriggerRewrites { get; set; } = [];

    /// <summary>
    /// Gets or sets external target references that are intentionally left unchanged.
    /// </summary>
    public List<string> ExternalReferences { get; set; } = [];

    /// <summary>
    /// Gets or sets whether trigger index remapping is available.
    /// </summary>
    public bool IsTriggerIndexRemapAvailable { get; set; }

    /// <summary>
    /// Gets or sets the trigger remap unavailable reason.
    /// </summary>
    public string? TriggerIndexRemapUnavailableReason { get; set; }
}

/// <summary>
/// Result returned after pasting one copied behavior.
/// </summary>
public sealed class FrontedBehaviorPasteResult
{
    /// <summary>
    /// Gets or sets whether the behavior was pasted.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the paste preview and compatibility result.
    /// </summary>
    public required FrontedBehaviorPastePreview Preview { get; set; }

    /// <summary>
    /// Gets or sets the pasted behavior when successful.
    /// </summary>
    public FrontedBehavior? Behavior { get; set; }
}

/// <summary>
/// Stores the current app-level behavior clipboard payload.
/// </summary>
public interface IFrontedBehaviorClipboard
{
    /// <summary>
    /// Gets the current clipboard payload.
    /// </summary>
    FrontedBehaviorClipboardPayload? Payload { get; }

    /// <summary>
    /// Replaces the current clipboard payload.
    /// </summary>
    /// <param name="payload">The payload to store.</param>
    void Set(FrontedBehaviorClipboardPayload payload);
}
