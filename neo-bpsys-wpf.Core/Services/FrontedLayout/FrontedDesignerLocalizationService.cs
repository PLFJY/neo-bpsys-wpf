using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Fallback Designer v3 localization service used when the host does not provide i18n resources.
/// </summary>
public class FrontedDesignerLocalizationService : IFrontedDesignerLocalizationService
{
    public virtual string GetPropertyDisplayName(string propertyName) =>
        GetLocalizedOrFallback($"Designer.Property.{propertyName}", propertyName);

    public virtual string GetPropertyDescription(string propertyName) =>
        GetLocalizedOrFallback($"Designer.PropertyDescription.{propertyName}", string.Empty);

    public virtual string GetGroupDisplayName(string groupName) =>
        GetLocalizedOrFallback($"Designer.PropertyGroup.{groupName}", groupName);

    public virtual string GetControlTypeDisplayName(string controlType) =>
        GetLocalizedOrFallback($"Designer.ControlType.{controlType}", controlType);

    public virtual string GetWindowDisplayName(string windowTypeName) =>
        GetLocalizedOrFallback($"Designer.Window.{windowTypeName}", windowTypeName);

    public virtual string GetCanvasDisplayName(string canvasName) =>
        GetLocalizedOrFallback($"Designer.Canvas.{canvasName}", canvasName);

    public virtual string GetOptionDisplayName(string propertyName, object? value)
    {
        var rawValue = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return GetLocalizedOrFallback($"Designer.Option.{propertyName}.{rawValue}", rawValue);
    }

    public virtual string GetBindingNodeDisplayName(string pathOrPropertyName, string? fullPath = null)
    {
        var key = string.IsNullOrWhiteSpace(fullPath) ? pathOrPropertyName : fullPath;
        return GetLocalizedOrFallback($"Designer.Binding.{key}", pathOrPropertyName);
    }

    public virtual string GetBindingTypeDisplayName(string typeName) =>
        GetLocalizedOrFallback($"Designer.BindingType.{typeName}", typeName);

    public virtual string GetDesignerText(string key, string fallback) =>
        GetLocalizedOrFallback(key, fallback);

    protected virtual string GetLocalizedOrFallback(string key, string fallback) => fallback;
}
