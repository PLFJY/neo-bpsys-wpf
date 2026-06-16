namespace neo_bpsys_wpf.Core.Models.SmartBpModule;

/// <summary>
/// Constants shared by the SmartBP host shell and runtime module.
/// </summary>
public static class SmartBpModuleConstants
{
    /// <summary>
    /// SmartBP module component identifier.
    /// </summary>
    public const string ComponentId = "SmartBpModule";

    /// <summary>
    /// Runtime ABI version expected by this app build.
    /// </summary>
    public const int RuntimeAbiVersion = 1;

    /// <summary>
    /// Windows x64 RID supported by the current module package.
    /// </summary>
    public const string Rid = "win-x64";

    /// <summary>
    /// Module entry assembly file name.
    /// </summary>
    public const string EntryAssemblyName = "neo-bpsys-wpf.SmartBp.Module.dll";

    /// <summary>
    /// SmartBP game data autofill feature command identifier.
    /// </summary>
    public const string AutoFillGameDataCommandId = "SmartBp.AutoFillGameData";
}
