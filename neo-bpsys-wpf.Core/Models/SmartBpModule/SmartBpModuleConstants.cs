namespace neo_bpsys_wpf.Core.Models.SmartBpModule;

/// <summary>
/// SmartBP 宿主外壳与运行时模块共享的常量。
/// </summary>
public static class SmartBpModuleConstants
{
    /// <summary>
    /// SmartBP 模块组件标识符。
    /// </summary>
    public const string ComponentId = "SmartBpModule";

    /// <summary>
    /// 本应用构建所期望的运行时 ABI 版本。
    /// </summary>
    public const int RuntimeAbiVersion = 1;

    /// <summary>
    /// 当前模块包支持的 Windows x64 RID。
    /// </summary>
    public const string Rid = "win-x64";

    /// <summary>
    /// 模块入口程序集文件名。
    /// </summary>
    public const string EntryAssemblyName = "neo-bpsys-wpf.SmartBp.Module.dll";

    /// <summary>
    /// SmartBP 赛后数据自动回填功能命令标识符。
    /// </summary>
    public const string AutoFillGameDataCommandId = "SmartBp.AutoFillGameData";
}
