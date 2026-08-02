using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Logging;
using neo_bpsys_wpf.ProductTour;
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
using Wpf.Ui;
using Wpf.Ui.Controls;

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
                // 自定义文件日志：当前运行始终写入 latest.txt，正常退出时按启动时间归档为 log-{时间}.txt
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
        SyncProductTourDebugState(settingsHostService.Settings);
        settingsHostService.SettingsChanged += (_, settings) => SyncProductTourDebugState(settings);
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

        MainWindow = (FluentWindow)IAppHost.Host.Services.GetRequiredService<INavigationWindow>();

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
        var host = IAppHost.Host;
        if (host is not null)
        {
            try
            {
                host.Services.GetService<ILogger<App>>()?.LogInformation("Application Closed");
                host.Services.GetService<IBpuiFileActivationService>()?.StopListening();
                await host.StopAsync();
            }
            finally
            {
                host.Dispose();
                if (ReferenceEquals(IAppHost.Host, host))
                {
                    IAppHost.Host = null;
                }
            }
        }

        // 正常退出时将 latest.txt 按启动时间归档；若未正常退出（崩溃），latest.txt 保留供下次启动归档
        FileLoggerProvider.FinalizeRun();

        base.OnExit(e);
    }

    /// <inheritdoc/>
    public override void Restart() => Restart(null);

    /// <summary>
    /// 重启应用程序，可附加额外的命令行参数。
    /// </summary>
    /// <param name="additionalArgs">要附加到新进程的命令行参数；为 <see langword="null"/> 时不附加额外参数，直接用当前进程参数重启。</param>
    public void Restart(string[]? additionalArgs)
    {
        var exePath = ResourceAssembly.Location.Replace(".dll", ".exe");

        // 保留当前进程的命令行参数（跳过可执行文件路径）
        var args = new List<string>(Environment.GetCommandLineArgs().Skip(1));

        if (additionalArgs is not null)
        {
            foreach (var arg in additionalArgs)
            {
                args.Add(arg);
            }
        }

        // 释放互斥锁，允许新进程获取单实例锁
        _mutex?.Close();

        // 启动新进程；UseShellExecute=false 与 CreateNoWindow=true 确保 WPF 进程不创建控制台窗口
        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
        Process.Start(startInfo);

        Current.Shutdown();
    }

    /// <summary>
    /// 以管理员权限（UAC 提升）重启应用程序。
    /// </summary>
    /// <remarks>
    /// 通过 ShellExecute 配合 <c>Verb=runas</c> 唤起 UAC 提权提示。
    /// 若用户在 UAC 提示中拒绝，将抛出 <see cref="System.ComponentModel.Win32Exception"/>
    /// （错误码 <c>ERROR_CANCELLED</c> = 1223），调用方应捕获并提示用户。
    /// 重启流程与 <see cref="Restart()"/> 一致：释放单实例互斥锁后启动新进程，再关闭当前进程。
    /// </remarks>
    /// <exception cref="System.ComponentModel.Win32Exception">
    /// 用户拒绝 UAC 提权，或系统无法启动提升后的进程。
    /// </exception>
    public void RestartAsAdmin()
    {
        var exePath = ResourceAssembly.Location.Replace(".dll", ".exe");

        // 保留当前进程的命令行参数（跳过可执行文件路径）
        var args = Environment.GetCommandLineArgs().Skip(1);

        // 释放互斥锁，允许新进程获取单实例锁
        _mutex?.Close();

        // UseShellExecute=true 配合 Verb=runas 唤起 UAC 提权提示；
        // 此时 ArgumentList 不可用，必须手动拼装并转义参数到 Arguments
        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = string.Join(' ', args.Select(QuoteArgument))
        };

        Process.Start(startInfo);

        Current.Shutdown();
    }

    /// <summary>
    /// 对命令行参数进行转义，使其在 <see cref="ProcessStartInfo.Arguments"/> 中保持原义。
    /// </summary>
    /// <param name="arg">原始参数。</param>
    /// <returns>转义后的参数。</returns>
    private static string QuoteArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return arg;
        return "\"" + arg.Replace("\"", "\\\"") + "\"";
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
    /// 将 <see cref="Settings.IsProductTourDebugEnabled"/> 同步到运行期的 <see cref="ProductTourOptions.IsDebugWindowEnabled"/>。
    /// 设置加载完成或用户在设置页切换后均调用此方法，使产品导览调试窗口的启停以 Settings 为准。
    /// </summary>
    /// <param name="settings">当前生效的应用设置。</param>
    public static void SyncProductTourDebugState(Settings settings)
    {
        IAppHost.Host?.Services.GetService<ProductTourOptions>()?.IsDebugWindowEnabled =
            settings.IsProductTourDebugEnabled;
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
