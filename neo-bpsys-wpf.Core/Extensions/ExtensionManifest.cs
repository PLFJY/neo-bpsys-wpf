namespace neo_bpsys_wpf.Core.Extensions;

/// <summary>
/// ExtensionInfo 类用于存储扩展的基本信息。
/// 该类包含扩展的数据文件夹路径、名称、版本、版本代码、作者和描述等属性。
/// 这些信息可以用于扩展的管理和显示。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class ExtensionManifest : Attribute
{
    /// <summary>
    /// 扩展数据文件夹名称，相关配置文件都理应存放在此文件夹下。
    /// 该数据文件夹实际路径为：
    /// %USERPROFILE%\AppData\Roaming\neo-bpsys-wpf\Extensions\`ExtensionDataFolder`\
    /// 请注意：若两个扩展的 ExtensionDataFolder 相同，则会导致数据冲突。
    /// </summary>
    public string ExtensionDataFolder { get; }
    /// <summary>
    /// 扩展名称
    /// </summary>
    public string ExtensionName { get; }
    /// <summary>
    /// 扩展版本号
    /// </summary>
    public string ExtensionVersion { get; }
    /// <summary>
    /// 扩展版本代码，每次更新扩展时应增加此版本代码。
    /// 该版本代码在未来可能用于判断扩展是否需要更新。
    /// </summary>
    public int ExtensionVersionCode { get; }
    /// <summary>
    /// 扩展作者名称
    /// </summary>
    public string ExtensionAuthor { get; }
    /// <summary>
    /// 扩展描述
    /// </summary>
    public string ExtensionDescription { get; }
    
    /// <summary>
    /// 使用该构造函数来初始化扩展的基本信息。
    /// </summary>
    /// <param name="extensionDataFolder"></param>
    /// <param name="extensionName"></param>
    /// <param name="extensionVersion"></param>
    /// <param name="extensionVersionCode"></param>
    /// <param name="extensionAuthor"></param>
    /// <param name="extensionDescription"></param>
    public ExtensionManifest(
        string extensionDataFolder,
        string extensionName,
        string extensionVersion,
        int extensionVersionCode,
        string extensionAuthor,
        string extensionDescription)
    {
        ExtensionDataFolder = extensionDataFolder;
        ExtensionName = extensionName;
        ExtensionVersion = extensionVersion;
        ExtensionVersionCode = extensionVersionCode;
        ExtensionAuthor = extensionAuthor;
        ExtensionDescription = extensionDescription;
    }
}