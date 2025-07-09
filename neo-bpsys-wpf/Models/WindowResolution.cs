using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Models;

public class WindowResolution(int width, int height)
{
    public int Width { get; set; } = width;

    public int Height { get; set; } = height;
}