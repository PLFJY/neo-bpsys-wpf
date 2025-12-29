using System.Collections.Concurrent;
using neo_bpsys_wpf.Core.Plugins.Services;
using neo_bpsys_wpf.Core.Plugins.UI;

namespace neo_bpsys_wpf.Plugins.Services;

/// <summary>
/// UI扩展服务实现
/// </summary>
public class UIExtensionService : IUIExtensionService
{
    private readonly ConcurrentDictionary<string, IUIExtensionPoint> _extensions = new();
    private readonly object _lock = new();

    /// <inheritdoc/>
    public event EventHandler<UIExtensionChangedEventArgs>? ExtensionChanged;

    /// <inheritdoc/>
    public void RegisterExtension(IUIExtensionPoint extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        lock (_lock)
        {
            if (_extensions.TryAdd(extension.Id, extension))
            {
                OnExtensionChanged(new UIExtensionChangedEventArgs
                {
                    ChangeType = UIExtensionChangeType.Added,
                    Extension = extension
                });
            }
            else
            {
                // 更新现有扩展
                _extensions[extension.Id] = extension;
                OnExtensionChanged(new UIExtensionChangedEventArgs
                {
                    ChangeType = UIExtensionChangeType.Updated,
                    Extension = extension
                });
            }
        }
    }

    /// <inheritdoc/>
    public void UnregisterExtension(string extensionId)
    {
        lock (_lock)
        {
            if (_extensions.TryRemove(extensionId, out var extension))
            {
                OnExtensionChanged(new UIExtensionChangedEventArgs
                {
                    ChangeType = UIExtensionChangeType.Removed,
                    Extension = extension
                });
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IUIExtensionPoint> GetExtensions(ExtensionPointLocation location)
    {
        return _extensions.Values
            .Where(e => e.Location == location && e.IsVisible)
            .OrderBy(e => e.Priority)
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IUIExtensionPoint> GetExtensions(string customLocationName)
    {
        return _extensions.Values
            .Where(e => e.Location == ExtensionPointLocation.Custom 
                        && e.CustomLocationName == customLocationName 
                        && e.IsVisible)
            .OrderBy(e => e.Priority)
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<T> GetExtensions<T>() where T : IUIExtensionPoint
    {
        return _extensions.Values
            .OfType<T>()
            .Where(e => e.IsVisible)
            .OrderBy(e => e.Priority)
            .ToList();
    }

    /// <inheritdoc/>
    public IUIExtensionPoint? GetExtension(string extensionId)
    {
        return _extensions.TryGetValue(extensionId, out var extension) ? extension : null;
    }

    /// <summary>
    /// 触发扩展变更事件
    /// </summary>
    protected virtual void OnExtensionChanged(UIExtensionChangedEventArgs e)
    {
        ExtensionChanged?.Invoke(this, e);
    }
}
