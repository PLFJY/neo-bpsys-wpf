namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

public abstract class ShapeFrontedControlConfigBase : FrontedControlConfigBase
{
    public ShapeFillMode FillMode { get; set; } = ShapeFillMode.Solid;

    /// <summary>
    /// Gets or sets whether gradient fill is enabled.
    /// When true, FillColor is used as the gradient start color
    /// and GradientEndColor becomes visible.
    /// </summary>
    public bool UseGradient { get; set; }

    public bool UseFillBinding { get; set; }

    public string? FillColor { get; set; } = "#FFFFFFFF";

    public string? FillBindingPath { get; set; }

    public bool UseGradientStartBinding { get; set; }

    public string? GradientStartColor { get; set; } = "#FFFFFFFF";

    public string? GradientStartBindingPath { get; set; }

    public bool UseGradientEndBinding { get; set; }

    public string? GradientEndColor { get; set; } = "#00FFFFFF";

    public string? GradientEndBindingPath { get; set; }

    private double _gradientAngle;

    public double GradientAngle
    {
        get => _gradientAngle;
        set
        {
            if (!double.IsFinite(value))
            {
                _gradientAngle = 0;
                return;
            }

            var normalized = value % 360D;
            _gradientAngle = normalized < 0 ? normalized + 360D : normalized;
        }
    }

    public string? StrokeColor { get; set; }

    public double StrokeThickness { get; set; }
}
