namespace neo_bpsys_wpf.Core.Extensions;

/// <summary>
/// ExtensionInfo 类用于存储插件的基本信息。
/// 该类包含插件的数据文件夹路径、名称、版本、版本代码、作者和描述等属性。
/// 这些信息可以用于插件的管理和显示。
/// </summary>
public class ExtensionInfo
{
    /// <summary>
    /// 插件数据文件夹名称，相关配置文件都理应存放在此文件夹下。
    /// 该数据文件夹实际路径为：
    /// %USERPROFILE%\AppData\Roaming\neo-bpsys-wpf\Extensions\`ExtensionDataFolder`\
    /// 请注意：若两个插件的 ExtensionDataFolder 相同，则会导致数据冲突。
    /// </summary>
    public String ExtensionDataFolder { get; internal set; } = "";
    /// <summary>
    /// 插件名称
    /// </summary>
    public String ExtensionName { get; internal set; } = "";
    /// <summary>
    /// 插件版本号
    /// </summary>
    public String ExtensionVersion { get; internal set; } = "";
    /// <summary>
    /// 插件版本代码，每次更新插件时应增加此版本代码。
    /// 该版本代码在未来可能用于判断插件是否需要更新。
    /// </summary>
    public int ExtensionVersionCode { get; internal set; } = 0;
    /// <summary>
    /// 插件作者名称
    /// </summary>
    public string ExtensionAuthor { get; internal set; } = "";
    /// <summary>
    /// 插件描述
    /// </summary>
    public string ExtensionDescription { get; internal set; } = "";
}