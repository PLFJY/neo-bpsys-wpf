using System.Windows.Input;

namespace neo_bpsys_wpf.Core.Plugins.Commands;

/// <summary>
/// 命令注册信息
/// </summary>
public sealed class CommandRegistration
{
    /// <summary>
    /// 命令唯一标识符
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 命令显示名称
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 命令描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 命令实例
    /// </summary>
    public required ICommand Command { get; init; }

    /// <summary>
    /// 命令图标（可选）
    /// </summary>
    public object? Icon { get; init; }

    /// <summary>
    /// 快捷键（可选）
    /// </summary>
    public KeyGesture? KeyGesture { get; init; }

    /// <summary>
    /// 命令分组
    /// </summary>
    public string Group { get; init; } = "Default";

    /// <summary>
    /// 所属插件ID
    /// </summary>
    public string? PluginId { get; internal set; }
}

/// <summary>
/// 命令扩展服务接口
/// </summary>
public interface ICommandExtensionService
{
    /// <summary>
    /// 注册命令
    /// </summary>
    /// <param name="registration">命令注册信息</param>
    void RegisterCommand(CommandRegistration registration);

    /// <summary>
    /// 取消注册命令
    /// </summary>
    /// <param name="commandId">命令ID</param>
    void UnregisterCommand(string commandId);

    /// <summary>
    /// 获取所有注册的命令
    /// </summary>
    /// <returns>命令注册信息列表</returns>
    IReadOnlyList<CommandRegistration> GetCommands();

    /// <summary>
    /// 获取指定分组的命令
    /// </summary>
    /// <param name="group">分组名称</param>
    /// <returns>命令注册信息列表</returns>
    IReadOnlyList<CommandRegistration> GetCommands(string group);

    /// <summary>
    /// 根据ID获取命令
    /// </summary>
    /// <param name="commandId">命令ID</param>
    /// <returns>命令注册信息</returns>
    CommandRegistration? GetCommand(string commandId);

    /// <summary>
    /// 执行命令
    /// </summary>
    /// <param name="commandId">命令ID</param>
    /// <param name="parameter">命令参数</param>
    /// <returns>是否执行成功</returns>
    bool ExecuteCommand(string commandId, object? parameter = null);

    /// <summary>
    /// 当命令注册时触发的事件
    /// </summary>
    event EventHandler<CommandRegistration>? CommandRegistered;

    /// <summary>
    /// 当命令取消注册时触发的事件
    /// </summary>
    event EventHandler<string>? CommandUnregistered;
}
