using System.IO;
using System.Reflection;

namespace neo_bpsys_wpf.Core;

/// <summary>
/// 应用程序常量
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// 应用程序名称
    /// </summary>
    public const string AppName = "neo-bpsys-wpf";

    public static readonly string AppVersion = Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "unknown";

    #region Paths

    /// <summary>
    /// 应用程序数据路径
    /// </summary>
    public static readonly string AppDataPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    /// <summary>
    /// 应用程序输出路径
    /// </summary>
    public static readonly string AppOutputPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppName);

    /// <summary>
    /// 配置文件路径
    /// </summary>
    public static readonly string ConfigFilePath = Path.Combine(AppDataPath, "Config.json");

    /// <summary>
    /// 应用程序临时数据路径
    /// </summary>
    public static readonly string AppTempPath = Path.Combine(Path.GetTempPath(), AppName);

    /// <summary>
    /// 自定义UI路径
    /// </summary>
    public static readonly string CustomUiPath = Path.Combine(AppDataPath, "CustomUi");

    /// <summary>
    /// 日志路径
    /// </summary>
    public static readonly string LogPath = Path.Combine(AppDataPath, "Log");

    /// <summary>
    /// 资源文件路径
    /// </summary>
    public static readonly string ResourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");

    /// <summary>
    /// 插件加载路径
    /// </summary>
    public static readonly string PluginPath = Path.Combine(AppDataPath, "Plugins");

    /// <summary>
    /// 内置插件加载路径
    /// </summary>
    public static readonly string BuiltInPluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");

    /// <summary>
    /// 
    /// </summary>
    public static readonly string PluginConfigsPath = Path.Combine(AppDataPath, "PluginConfigs");

    #endregion

    #region Counts

    /// <summary>
    /// 当局求生者Ban位数量
    /// </summary>
    public const int CurrentBanSurCount = 4;

    /// <summary>
    /// 当局监管者Ban位数量
    /// </summary>
    public const int CurrentBanHunCount = 2;

    /// <summary>
    /// 全局求生者Ban位数量
    /// </summary>
    public const int GlobalBanSurCount = 12;

    /// <summary>
    /// 全局监管者Ban位数量
    /// </summary>
    public const int GlobalBanHunCount = 3;

    #endregion
}