namespace neo_bpsys_wpf.Core.Models.SmartBpModule;

/// <summary>
/// 提供 SmartBP 模块本地版本低于要求版本时的事件数据。
/// </summary>
/// <param name="LocalVersion">本地已加载模块的版本号。</param>
/// <param name="RequiredVersion">远程发布标签要求的最小兼容版本号。</param>
public sealed record ModuleVersionOutdatedEventArgs(string LocalVersion, string RequiredVersion);
