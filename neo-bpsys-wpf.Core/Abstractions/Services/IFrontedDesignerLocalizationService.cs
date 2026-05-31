namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Provides display-layer localization for Designer v3 without changing layout contracts.
/// </summary>
public interface IFrontedDesignerLocalizationService
{
    string GetPropertyDisplayName(string propertyName);

    string GetPropertyDescription(string propertyName);

    string GetGroupDisplayName(string groupName);

    string GetControlTypeDisplayName(string controlType);

    string GetWindowDisplayName(string windowTypeName);

    string GetCanvasDisplayName(string canvasName);

    string GetOptionDisplayName(string propertyName, object? value);

    string GetBindingNodeDisplayName(string pathOrPropertyName, string? fullPath = null);

    string GetBindingTypeDisplayName(string typeName);
}
