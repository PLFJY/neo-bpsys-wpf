using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Models;

public class Settings
{
    public FrontResolution FrontResolution { get; set; } = new();
}

public class FrontResolution
{
    public WindowResolution BpWindow { get; set; } = new();
    public WindowResolution InterludeWindow { get; set; } = new();
    public WindowResolution GameDataWindow { get; set; } = new();
}