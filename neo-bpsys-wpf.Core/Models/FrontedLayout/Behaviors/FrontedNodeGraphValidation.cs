namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public enum FrontedNodeGraphValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed class FrontedNodeGraphValidationMessage
{
    public FrontedNodeGraphValidationSeverity Severity { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    public Guid? NodeId { get; init; }
    public Guid? ConnectionId { get; init; }
    public string? PropertyName { get; init; }
}
