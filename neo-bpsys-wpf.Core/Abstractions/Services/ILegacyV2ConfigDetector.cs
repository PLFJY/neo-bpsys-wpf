namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Detects legacy v2 application settings JSON.
/// </summary>
public interface ILegacyV2ConfigDetector
{
    /// <summary>
    /// Returns whether the supplied config JSON uses the legacy v2 settings shape.
    /// </summary>
    /// <param name="configJson">Config JSON text.</param>
    /// <returns><see langword="true"/> when the JSON should be migrated from v2.</returns>
    /// <exception cref="System.Text.Json.JsonException">Thrown when <paramref name="configJson"/> is not valid JSON.</exception>
    bool IsLegacyV2Config(string configJson);
}
