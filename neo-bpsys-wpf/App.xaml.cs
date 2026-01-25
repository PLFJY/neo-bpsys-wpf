using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Themes;
using Serilog;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : AppBase
{
    /// <summary>
    /// 互斥锁
    /// </summary>
    private static Mutex? _mutex;

    private bool _createdNew;

    protected override async void OnStartup(StartupEventArgs e)
    {
        //Log编码修正
        Console.OutputEncoding = Encoding.UTF8;

        CurrentLifetime = ApplicationLifetime.Initializing;
        //保证只运行一个实例
        _mutex = new Mutex(true, AppConstants.AppName, out _createdNew);
        if (!_createdNew)
        {
            _ = MessageBoxHelper.ShowInfoAsync("程序已在运行\nThe program is already running",
                "Warning");
            Current.Shutdown();
        }

        IAppHost.Host = Host
            .CreateDefaultBuilder()
            .UseSerilog((_, loggerConfiguration) =>
            {
                if (!Directory.Exists(AppConstants.LogPath))
                    Directory.CreateDirectory(AppConstants.LogPath);

                loggerConfiguration
                    .WriteTo.Console()
                    .WriteTo.File(
                        path: Path.Combine(AppConstants.LogPath, "log-.txt"), // 使用日期滚动的文件名格式
                        rollingInterval: RollingInterval.Hour, // 小时创建一个新文件
                        retainedFileCountLimit: 3, // 只保留最近3天的日志文件
                        outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        encoding: Encoding.UTF8
                    )
                    .Enrich.FromLogContext()
                    .MinimumLevel.Debug();
            })
            .ConfigureLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog(dispose: true);
            })
            .ConfigureServices(ConfigureServices)
            .Build();

        base.OnStartup(e);
        //设置动画帧率
        Timeline.DesiredFrameRateProperty.OverrideMetadata(
            typeof(Timeline),
            new FrameworkPropertyMetadata { DefaultValue = 100 }
        );

        //启动初始化log
        var logger = IAppHost.Host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Application Started");

        CurrentLifetime = ApplicationLifetime.StartingOffline;
        //读取设置
        var settingsHostService = IAppHost.Host.Services.GetRequiredService<ISettingsHostService>();
        await settingsHostService.LoadConfig();

        CurrentLifetime = ApplicationLifetime.StartingOnline;
        //添加不同颜色的icon到resources里面
        Current.Resources["scoreGlobal_surIcon"] = ImageHelper.GetUiImageSource(
            settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled
                ? "surIcon_black"
                : "surIcon");
        Current.Resources["scoreGlobal_hunIcon"] = ImageHelper.GetUiImageSource(
            settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled
                ? "hunIcon_black"
                : "hunIcon");
        Current.Resources["mapBpV2_surIcon"] = ImageHelper.GetUiImageSource(
            settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled
                ? "surIcon_black"
                : "surIcon");
        Current.Resources["mapBpV2_hunIcon"] = ImageHelper.GetUiImageSource(
            settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled
                ? "hunIcon_black"
                : "hunIcon");
        //设置图标切换跟随主题
        ApplicationThemeManager.Changed += (currentApplicationTheme, _) =>
        {
            foreach (var dict in Current.Resources.MergedDictionaries)
            {
                if (dict is not IconThemesDictionary iconThemesDictionary) continue;
                iconThemesDictionary.Theme = currentApplicationTheme;
                break;
            }
        };
        //主题初始化为深色
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        //设置语言
        var settingService = IAppHost.Host.Services.GetRequiredService<ISettingsHostService>();
        LocalizeDictionary.Instance.Culture = settingService.Settings.CultureInfo;
        Application.Current.Resources["CurrentLanguage"] = XmlLanguage.GetLanguage(settingService.Settings.CultureInfo.Name);

        //启动host
        await IAppHost.Host.StartAsync();

        CurrentLifetime = ApplicationLifetime.Running;

#if !DEBUG
        logger.LogInformation("Update checking on start up");
        await IAppHost.Host.Services.GetRequiredService<IUpdaterService>().UpdateCheck(true);
#endif
    }



    protected override async void OnExit(ExitEventArgs e)
    {
        CurrentLifetime = ApplicationLifetime.Stopping;
        base.OnExit(e);
        var logger = IAppHost.Host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Application Closed");
        await IAppHost.Host.StopAsync();
        IAppHost.Host.Dispose();
    }

    /// <inheritdoc/>
    public override void Restart()
    {
        // 释放互斥锁
        _mutex?.Close();
        // 重启应用程序
        System.Diagnostics.Process.Start(ResourceAssembly.Location.Replace(".dll", ".exe"));
        Current.Shutdown();
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e
    )
    {
        var logger = IAppHost.Host.Services.GetRequiredService<ILogger<App>>();
        logger.LogError("Application crashed unexpectedly");
#if !DEBUG
        _ = MessageBoxHelper.ShowInfoAsync($"出现了些在意料之外的错误，请带着下方地址处的日志文件联系开发者解决\nSome unexpected errors have occurred. Please contact the developer with the log file below for resolution \n\n{AppConstants.LogPath}\n ", "Error");
#endif
        // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
    }

    /// <inheritdoc/>
    public override void ShutDown()
    {
        Current.Shutdown();
    }

    /// <inheritdoc/>
    public override event EventHandler? AppStarted;

    /// <inheritdoc/>
    public override event EventHandler? AppStopping;
}