using System.Collections.Concurrent;
using System.Windows.Input;

namespace neo_bpsys_wpf.Core.Plugins.Commands;

/// <summary>
/// 命令扩展服务实现
/// </summary>
public sealed class CommandExtensionService : ICommandExtensionService
{
    private readonly ConcurrentDictionary<string, CommandRegistration> _commands = new();

    /// <inheritdoc/>
    public event EventHandler<CommandRegistration>? CommandRegistered;

    /// <inheritdoc/>
    public event EventHandler<string>? CommandUnregistered;

    /// <inheritdoc/>
    public void RegisterCommand(CommandRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (string.IsNullOrWhiteSpace(registration.Id))
        {
            throw new ArgumentException("Command ID cannot be null or empty.", nameof(registration));
        }

        // 如果命令已注册，先卸载旧的（幂等性处理）
        if (_commands.ContainsKey(registration.Id))
        {
            UnregisterCommand(registration.Id);
        }

        _commands[registration.Id] = registration;
        CommandRegistered?.Invoke(this, registration);
    }

    /// <inheritdoc/>
    public void UnregisterCommand(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            throw new ArgumentException("Command ID cannot be null or empty.", nameof(commandId));
        }

        if (_commands.TryRemove(commandId, out _))
        {
            CommandUnregistered?.Invoke(this, commandId);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<CommandRegistration> GetCommands()
    {
        return _commands.Values.ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<CommandRegistration> GetCommands(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return Array.Empty<CommandRegistration>();
        }

        return _commands.Values
            .Where(c => c.Group == group)
            .ToList();
    }

    /// <inheritdoc/>
    public CommandRegistration? GetCommand(string commandId)
    {
        return _commands.TryGetValue(commandId, out var registration) ? registration : null;
    }

    /// <inheritdoc/>
    public bool ExecuteCommand(string commandId, object? parameter = null)
    {
        if (!_commands.TryGetValue(commandId, out var registration))
        {
            return false;
        }

        if (!registration.Command.CanExecute(parameter))
        {
            return false;
        }

        try
        {
            registration.Command.Execute(parameter);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 按插件ID取消注册所有命令
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    public void UnregisterAllByPluginId(string pluginId)
    {
        var commandsToRemove = _commands.Values
            .Where(c => c.PluginId == pluginId)
            .Select(c => c.Id)
            .ToList();

        foreach (var id in commandsToRemove)
        {
            UnregisterCommand(id);
        }
    }
}
