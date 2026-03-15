namespace neo_bpsys_wpf.Models.Plugins;

public class PluginApiCompatibilityResult
{
    public bool IsCompatible { get; init; }

    public bool IsFormatValid { get; init; }

    public bool IsTooHigh { get; init; }

    public bool IsTooLow { get; init; }

    public string Message { get; init; } = string.Empty;
}
