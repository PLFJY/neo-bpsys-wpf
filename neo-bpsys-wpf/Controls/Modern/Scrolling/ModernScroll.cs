using System.Windows;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

public static class ModernScroll
{
    public static readonly DependencyProperty OwnershipProperty =
        DependencyProperty.RegisterAttached(
            "Ownership",
            typeof(ModernScrollOwnership),
            typeof(ModernScroll),
            new FrameworkPropertyMetadata(ModernScrollOwnership.Auto, FrameworkPropertyMetadataOptions.Inherits));

    public static ModernScrollOwnership GetOwnership(DependencyObject obj) =>
        (ModernScrollOwnership)obj.GetValue(OwnershipProperty);

    public static void SetOwnership(DependencyObject obj, ModernScrollOwnership value) =>
        obj.SetValue(OwnershipProperty, value);
}
