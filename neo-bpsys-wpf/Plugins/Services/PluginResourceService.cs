using System.IO;
using neo_bpsys_wpf.Core.Plugins.Services;

namespace neo_bpsys_wpf.Plugins.Services;

/// <summary>
/// 插件资源服务实现
/// </summary>
public class PluginResourceService : IPluginResourceService
{
    private readonly string _pluginsDirectory;
    private readonly string _dataDirectory;

    public PluginResourceService(string pluginsDirectory, string dataDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
        _dataDirectory = dataDirectory;

        if (!Directory.Exists(_pluginsDirectory))
            Directory.CreateDirectory(_pluginsDirectory);

        if (!Directory.Exists(_dataDirectory))
            Directory.CreateDirectory(_dataDirectory);
    }

    /// <inheritdoc/>
    public string GetResourceDirectory(string pluginId)
    {
        return Path.Combine(_pluginsDirectory, pluginId, "Resources");
    }

    /// <inheritdoc/>
    public string GetDataDirectory(string pluginId)
    {
        var path = Path.Combine(_dataDirectory, pluginId);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    /// <inheritdoc/>
    public string GetConfigFilePath(string pluginId)
    {
        return Path.Combine(GetDataDirectory(pluginId), "config.json");
    }

    /// <inheritdoc/>
    public async Task<byte[]> ReadResourceAsync(string pluginId, string relativePath)
    {
        var fullPath = Path.Combine(GetResourceDirectory(pluginId), relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Resource not found: {relativePath}", fullPath);
        
        return await File.ReadAllBytesAsync(fullPath);
    }

    /// <inheritdoc/>
    public async Task<string> ReadResourceTextAsync(string pluginId, string relativePath)
    {
        var fullPath = Path.Combine(GetResourceDirectory(pluginId), relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Resource not found: {relativePath}", fullPath);
        
        return await File.ReadAllTextAsync(fullPath);
    }

    /// <inheritdoc/>
    public async Task WriteDataAsync(string pluginId, string relativePath, byte[] data)
    {
        var fullPath = Path.Combine(GetDataDirectory(pluginId), relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(fullPath, data);
    }

    /// <inheritdoc/>
    public async Task WriteDataTextAsync(string pluginId, string relativePath, string text)
    {
        var fullPath = Path.Combine(GetDataDirectory(pluginId), relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(fullPath, text);
    }

    /// <inheritdoc/>
    public bool ResourceExists(string pluginId, string relativePath)
    {
        var fullPath = Path.Combine(GetResourceDirectory(pluginId), relativePath);
        return File.Exists(fullPath);
    }

    /// <inheritdoc/>
    public bool DataExists(string pluginId, string relativePath)
    {
        var fullPath = Path.Combine(GetDataDirectory(pluginId), relativePath);
        return File.Exists(fullPath);
    }
}
