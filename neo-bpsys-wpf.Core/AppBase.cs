using neo_bpsys_wpf.Core.Enums;
using System.Windows;

namespace neo_bpsys_wpf.Core;

public abstract class AppBase : Application, IAppHost
{
    /// <summary>
    /// 获取当前应用程序实例。
    /// </summary>
    public new static AppBase Current => (Application.Current as AppBase)!;

    /// <summary>
    /// 重启应用程序。
    /// </summary>
    public abstract void Restart();

    /// <summary>
    /// 停止当前应用程序。
    /// </summary>
    public abstract void ShutDown();

    /// <summary>
    /// 当应用启动时触发。
    /// </summary>
    public abstract event EventHandler? AppStarted;

    /// <summary>
    /// 当应用正在停止时触发。
    /// </summary>
    public abstract event EventHandler? AppStopping;

    /// <summary>
    /// 应用当前生命周期状态
    /// </summary>
    public static ApplicationLifetime CurrentLifetime { get; internal set; } = ApplicationLifetime.None;

    /// <summary>
    /// 应用当前的主窗口
    /// </summary>
    public new Window? MainWindow { get; internal set; } 
}