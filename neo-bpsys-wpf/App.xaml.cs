using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Logging;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Themes;
using neo_bpsys_wpf.Tutorial;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf;

/// <summary>
/// App.xaml 的交互逻辑。
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
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException(
                "neo-bpsys-wpf requires an x64 process. The current process is not 64-bit.");
        }

        //Log编码修正
        Console.OutputEncoding = Encoding.UTF8;

        CurrentLifetime = ApplicationLifetime.Initializing;
        //保证只运行一个实例
        _mutex = new Mutex(true, AppConstants.AppName, out _createdNew);
        if (!_createdNew)
        {
            var startupPackagePath = FindStartupBpuiPackagePath(e.Args);
            if (!string.IsNullOrWhiteSpace(startupPackagePath)
                && await TryForwardStartupBpuiPathAsync(startupPackagePath))
            {
                Current.Shutdown();
                return;
            }

            _ = MessageBoxHelper.ShowInfoAsync("程序已在运行\nThe program is already running",
                "Warning");
            Current.Shutdown();
            return;
        }

        IAppHost.Host = Host
            .CreateDefaultBuilder(e.Args)
            .ConfigureLogging(loggingBuilder =>
            {
                if (!Directory.Exists(AppConstants.LogPath))
                    Directory.CreateDirectory(AppConstants.LogPath);

                loggingBuilder.ClearProviders();
                // 自定义文件日志：每次启动创建带时间戳的新文件，保留最近 10 次运行
                loggingBuilder.AddProvider(new FileLoggerProvider(AppConstants.LogPath, GetInitialAppLogLevel()));
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
        IAppHost.Host.Services
            .GetRequiredService<IBpuiFileAssociationService>()
            .EnsureAssociationState(settingsHostService.Settings.AssociateBpuiFiles);
        ApplyLogLevel(settingsHostService.Settings.LogLevel);
        IAppHost.Host.Services.GetRequiredService<FrontedSharedDataBehaviorEventBridge>().Start();
        _ = IAppHost.Host.Services.GetRequiredService<IFrontedBehaviorEventDebugService>();

        CurrentLifetime = ApplicationLifetime.StartingOnline;
        //添加不同颜色的icon到resources里面
        Current.Resources["scoreGlobal_surIcon"] = ImageHelper.GetUiImageSource("surIcon");
        Current.Resources["scoreGlobal_hunIcon"] = ImageHelper.GetUiImageSource("hunIcon");
        Current.Resources["mapBpV2_surIcon"] = ImageHelper.GetUiImageSource("surIcon");
        Current.Resources["mapBpV2_hunIcon"] = ImageHelper.GetUiImageSource("hunIcon");
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
        ApplicationThemeManager.Apply(ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.Mica);

        //设置语言
        var settingService = IAppHost.Host.Services.GetRequiredService<ISettingsHostService>();
        LocalizeDictionary.Instance.Culture = settingService.Settings.CultureInfo;
        Application.Current.Resources["CurrentLanguage"] = XmlLanguage.GetLanguage(settingService.Settings.CultureInfo.Name);
        ProductTourFontResourceHelper.Apply(settingService.Settings.CultureInfo);

        //启动host
        await IAppHost.Host.StartAsync();
        var bpuiFileActivationService = IAppHost.Host.Services.GetRequiredService<IBpuiFileActivationService>();
        bpuiFileActivationService.StartListening();
        var initialPackagePath = FindStartupBpuiPackagePath(e.Args);
        if (!string.IsNullOrWhiteSpace(initialPackagePath))
        {
            _ = bpuiFileActivationService.OpenPackageAsync(initialPackagePath);
        }

        AppStarted?.Invoke(this, EventArgs.Empty);

        CurrentLifetime = ApplicationLifetime.Running;

#if !DEBUG && !PREVIEW
        logger.LogInformation("Update checking on start up");
        await IAppHost.Host.Services.GetRequiredService<IUpdaterService>().UpdateCheck(true);
#endif
    }



    protected override async void OnExit(ExitEventArgs e)
    {
        CurrentLifetime = ApplicationLifetime.Stopping;
        AppStopping?.Invoke(this, EventArgs.Empty);
        var logger = IAppHost.Host!.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Application Closed");
        IAppHost.Host.Services.GetRequiredService<IBpuiFileActivationService>().StopListening();
        await IAppHost.Host.StopAsync();
        IAppHost.Host.Dispose();
        base.OnExit(e);
    }

    /// <inheritdoc/>
    public override void Restart()
    {
        // 释放互斥锁
        _mutex?.Close();
        // 重启应用程序
        Process.Start(ResourceAssembly.Location.Replace(".dll", ".exe"));
        Current.Shutdown();
    }

    /// <summary>
    /// 当应用抛出异常但未被处理时发生。
    /// </summary>
    private async void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e
    )
    {
        var logger = IAppHost.Host!.Services.GetRequiredService<ILogger<App>>();
        logger.LogError("Application crashed unexpectedly");
        logger.LogError(e.Exception.Message);
#if !DEBUG
        await MessageBoxHelper.ShowInfoAsync($"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "UnexpectedExceptionMessage")}\n\n{AppConstants.LogPath}\n ", "Error");
        Process.Start("explorer.exe", AppConstants.LogPath);
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

    /// <summary>
    /// 立即应用新的日志级别。
    /// </summary>
    public static void ApplyLogLevel(AppLogLevel logLevel)
    {
        FileLoggerProvider.SetLevel(GetEffectiveLogLevel(logLevel));
    }

    /// <summary>
    /// 获取当前构建实际使用的日志级别。
    /// </summary>
    /// <param name="configuredLevel">用户配置的日志级别。</param>
    /// <returns>当前构建实际生效的日志级别。</returns>
    public static AppLogLevel GetEffectiveLogLevel(AppLogLevel configuredLevel)
    {
#if DEBUG || PREVIEW
        return AppLogLevel.Information;
#else
        return configuredLevel;
#endif
    }

    /// <summary>
    /// 获取当前构建是否允许用户修改日志级别。
    /// </summary>
    public static bool IsLogLevelUserConfigurable
    {
        get
        {
#if DEBUG || PREVIEW
            return false;
#else
            return true;
#endif
        }
    }

    /// <summary>
    /// 从 <see cref="AppConstants.ConfigFilePath"/> 读取持久化的日志级别；读取失败时返回 <see cref="AppLogLevel.Warning"/>。
    /// </summary>
    /// <returns>持久化的应用日志级别。</returns>
    private static AppLogLevel GetInitialAppLogLevel()
    {
        try
        {
            if (!File.Exists(AppConstants.ConfigFilePath))
            {
                return GetEffectiveLogLevel(AppLogLevel.Warning);
            }

            using var stream = File.OpenRead(AppConstants.ConfigFilePath);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("LogLevel", out var levelElement))
            {
                return GetEffectiveLogLevel(AppLogLevel.Warning);
            }

            var levelText = levelElement.GetString();
            return GetEffectiveLogLevel(
                Enum.TryParse<AppLogLevel>(levelText, ignoreCase: true, out var logLevel)
                    ? logLevel
                    : AppLogLevel.Warning);
        }
        catch
        {
            return GetEffectiveLogLevel(AppLogLevel.Warning);
        }
    }

    private static string? FindStartupBpuiPackagePath(IEnumerable<string> args)
    {
        return args
            .Select(arg => arg.Trim().Trim('"'))
            .FirstOrDefault(arg => string.Equals(Path.GetExtension(arg), ".bpui", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<bool> TryForwardStartupBpuiPathAsync(string packagePath)
    {
        try
        {
            await using var pipe = new System.IO.Pipes.NamedPipeClientStream(
                ".",
                AppConstants.AppName + ".bpui-open",
                System.IO.Pipes.PipeDirection.Out,
                System.IO.Pipes.PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1500);
            await using var writer = new StreamWriter(pipe) { AutoFlush = true };
            await writer.WriteLineAsync(packagePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            return false;
        }
    }
}
