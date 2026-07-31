using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Helpers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WPFLocalizeExtension.Extensions;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 前台窗口服务，负责与前台窗口进行交互。
/// </summary>
public class FrontedWindowService : IFrontedWindowService
{
#if DEBUG
    static FrontedWindowService()
    {
        Debug.WriteLine($"[DIAG] FrontedWindowService: static ctor at {DateTimeOffset.Now:HH:mm:ss.fff}");
    }
#endif
    private readonly IServiceProvider _services;
    private readonly IFrontedWindowRegistry _windowRegistry;
    private readonly IFrontedWindowLayoutOptionsService _windowLayoutOptionsService;
    private readonly ILogger<FrontedWindowService> _logger;
    private readonly IFrontedEventBus? _eventBus;

    /// <summary>
    /// 前台窗口可变字典（私有），键为窗口 Canonical ID。使用 <see cref="StringComparer.OrdinalIgnoreCase"/>
    /// 与注册表的比较语义保持一致，避免调用方传入大小写不同的 ID 时无法命中缓存。
    /// </summary>
    private readonly Dictionary<string, Window> _frontedWindows = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 前台窗口状态可变字典（私有），键为窗口 Canonical ID，值为窗口是否可见。
    /// 比较语义与 <see cref="_frontedWindows"/> 一致。
    /// </summary>
    private readonly Dictionary<string, bool> _frontedWindowStates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取前台窗口的只读视图，键为窗口 Canonical ID。使用 <see cref="StringComparer.OrdinalIgnoreCase"/>
    /// 与注册表的比较语义保持一致，避免调用方传入大小写不同的 ID 时无法命中缓存。
    /// </summary>
    /// <remarks>
    /// 公开为 <see cref="IReadOnlyDictionary{TKey, TValue}"/> 以防止外部消费者直接修改缓存。
    /// 需要修改窗口缓存必须通过服务方法（如 <see cref="EnsureWindowCreated"/>、
    /// <see cref="ShowWindow(string)"/>、<see cref="HideWindow(string)"/> 等）。
    /// </remarks>
    public IReadOnlyDictionary<string, Window> FrontedWindows => _frontedWindows;

    /// <summary>
    /// 获取前台窗口状态的只读视图，键为窗口 Canonical ID，值为窗口是否可见。
    /// 比较语义与 <see cref="FrontedWindows"/> 一致。
    /// </summary>
    /// <remarks>
    /// 公开为 <see cref="IReadOnlyDictionary{TKey, TValue}"/> 以防止外部消费者直接修改状态缓存。
    /// 需要修改窗口状态必须通过服务方法。
    /// </remarks>
    public IReadOnlyDictionary<string, bool> FrontedWindowStates => _frontedWindowStates;

    /// <summary>
    /// 初始化前台窗口服务。
    /// </summary>
    /// <param name="services">服务提供者。</param>
    /// <param name="windowRegistry">窗口注册表。</param>
    /// <param name="windowLayoutOptionsService">窗口布局选项服务。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="eventBus">前台事件总线（可选）。</param>
    public FrontedWindowService(
        IServiceProvider services,
        IFrontedWindowRegistry windowRegistry,
        IFrontedWindowLayoutOptionsService windowLayoutOptionsService,
        ILogger<FrontedWindowService> logger,
        IFrontedEventBus? eventBus = null)
    {
        _services = services;
        _windowRegistry = windowRegistry;
        _windowLayoutOptionsService = windowLayoutOptionsService;
        _logger = logger;
        _eventBus = eventBus;
        if (!Directory.Exists(AppConstants.AppDataPath))
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
        }

#if DEBUG
        Debug.WriteLine($"[DIAG] FrontedWindowService: lazy fronted window creation enabled at {DateTimeOffset.Now:HH:mm:ss.fff}");
#endif
    }

    /// <summary>
    /// 将调用方传入的窗口 ID 规范化为注册表中的 Canonical ID。
    /// </summary>
    /// <param name="windowId">调用方传入的窗口 ID（可能是任意大小写）。</param>
    /// <returns>
    /// 当 <paramref name="windowId"/> 在注册表中存在时，返回 <see cref="FrontedWindowRegistration.Id"/>；
    /// 否则返回原始 <paramref name="windowId"/>，由调用方按未注册路径处理。
    /// </returns>
    /// <remarks>
    /// 该方法保证整条调用链只使用注册表中的 Canonical ID 作为缓存键和事件 payload，
    /// 避免调用方传入的大小写变体导致缓存孤立或事件 payload 不一致。
    /// </remarks>
    private string NormalizeWindowId(string windowId)
    {
        return _windowRegistry.TryGet(windowId, out var registration)
            ? registration.Id
            : windowId;
    }

    /// <inheritdoc/>
    public Window? EnsureWindowCreated(string windowId)
    {
        // 入口先规范化为 Canonical ID，整条调用链使用规范化后的值作为缓存键。
        var canonicalId = NormalizeWindowId(windowId);

        if (_frontedWindows.TryGetValue(canonicalId, out var existingWindow))
        {
            return existingWindow;
        }

        if (!_windowRegistry.TryGet(canonicalId, out var registration))
        {
            return null;
        }

        try
        {
            var window = CreateWindow(registration);
            RegisterFrontedWindow(registration.Id, window);
            return window;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create fronted window {WindowId}.",
                registration.Id);
            return null;
        }
    }

    private void RegisterFrontedWindow(string windowId, Window window)
    {
        if (_frontedWindows.TryAdd(windowId, window))
        {
            _frontedWindowStates[windowId] = false;
        }
    }

    /// <summary>
    /// 根据窗口注册的承载方式创建对应的前台窗口实例。
    /// </summary>
    /// <param name="registration">窗口注册，携带承载方式（Xaml/V3Layout）和来源信息。</param>
    /// <returns>创建的 <see cref="Window"/> 实例。</returns>
    /// <exception cref="InvalidOperationException">当 <paramref name="registration"/> 不是
    /// <see cref="FrontedV3LayoutWindowRegistration"/> 或 <see cref="FrontedXamlWindowRegistration"/> 时抛出。
    /// 系统只有这两个 sealed registration 模型，未知类型表示编程错误。</exception>
    /// <remarks>
    /// 分派依据 <see cref="FrontedWindowRegistration.Kind"/>：
    /// <list type="number">
    ///   <item><description><see cref="FrontedWindowRegistrationKind.V3Layout"/> —
    /// v3 layout host 窗口（含内置 v3 窗口和插件 v3 窗口），走 <see cref="CreateV3LayoutHostWindow"/>。</description></item>
    ///   <item><description><see cref="FrontedWindowRegistrationKind.Xaml"/> —
    /// XAML 窗口（含内置与插件），通过 DI 解析窗口实例。DataContext 已由
    /// <c>AddFrontedWindow</c> 注册 factory 设置，此处不再重复处理。</description></item>
    ///   <item><description>其他未知类型 — 抛出 <see cref="InvalidOperationException"/>，
    /// 系统只有两个 sealed registration 模型，未知类型表示编程错误。</description></item>
    /// </list>
    /// </remarks>
    private Window CreateWindow(FrontedWindowRegistration registration)
    {
        return registration switch
        {
            // 模式 1：v3 layout host 窗口（含内置 v3 窗口和插件 v3 窗口）
            FrontedV3LayoutWindowRegistration v3 => CreateV3LayoutHostWindow(v3),

            // 模式 2：XAML 窗口（含内置与插件）— 通过 DI 解析窗口实例。
            // DataContext 已由 AddFrontedWindow 注册 factory 设置，此处不再重复处理。
            FrontedXamlWindowRegistration xaml => CreateXamlWindow(xaml.WindowType),

            // 模式 3：系统只有两个 sealed registration 模型，未知类型表示编程错误。
            _ => throw new InvalidOperationException(
                $"Unsupported registration type: {registration.GetType().Name}. " +
                $"Only {nameof(FrontedV3LayoutWindowRegistration)} and {nameof(FrontedXamlWindowRegistration)} are supported.")
        };
    }

    private Window CreateV3LayoutHostWindow(FrontedWindowRegistration registration)
    {
        var window = new FrontedWindowBase();
        // 只向渲染层传递渲染所需的最小信息（Canonical ID 和显示名），
        // 不传递整个 registration，避免 Registry/UI 元数据泄漏到渲染层。
        // 显示名使用 Core 回退解析（DisplayName 为空时回退到 LocalId），
        // 内置窗口的本地化显示名由 UI 层通过 resx 覆盖。
        var displayName = FrontedWindowDisplayNameResolver.GetFallbackDisplayName(registration);
        window.InitializeV3LayoutHost(
            registration.Id,
            displayName,
            _services.GetRequiredService<IFrontedLayoutService>(),
            _services.GetRequiredService<IFrontedRenderer>(),
            _services.GetRequiredService<ISharedDataService>(),
            _services.GetService<IFrontedBehaviorRuntime>(),
            _services.GetService<ILogger<FrontedWindowBase>>());

        BindBuiltInV3WindowTitle(window, registration);
        return window;
    }

    private static void BindBuiltInV3WindowTitle(
        FrontedWindowBase window,
        FrontedWindowRegistration registration)
    {
        if (!registration.IsBuiltIn)
        {
            return;
        }

        var localizationKey =
            $"neo-bpsys-wpf:neo_bpsys_wpf.Locales.Designer:Designer.Window.{registration.LocalId}";
        var titleLocalization = new LocExtension(localizationKey);
        _ = titleLocalization.SetBinding(window, Window.TitleProperty);
    }

    /// <summary>
    /// 通过 DI 解析 XAML 前台窗口实例。
    /// </summary>
    /// <param name="windowType">WPF 窗口 CLR 类型，必须可赋值给 <see cref="Window"/>。</param>
    /// <returns>由 DI 容器提供的 <see cref="Window"/> 实例，DataContext 已由
    /// <c>AddFrontedWindow</c> 注册 factory 设置。</returns>
    /// <exception cref="InvalidOperationException">当 <paramref name="windowType"/> 为 <see langword="null"/>
    /// 或不可赋值给 <see cref="Window"/> 时抛出。</exception>
    /// <remarks>
    /// 仅通过 <see cref="ServiceProviderServiceExtensions.GetRequiredService(IServiceProvider, Type)"/> 解析窗口实例，
    /// 不再使用 <c>ActivatorUtilities.CreateInstance</c> fallback，避免掩盖 DI 配置错误。
    /// ViewModel 与 DataContext 的关联由 <c>AddFrontedWindow</c> 注册 factory 一次性完成。
    /// </remarks>
    private Window CreateXamlWindow(Type windowType)
    {
        if (windowType is null || !typeof(Window).IsAssignableFrom(windowType))
        {
            throw new InvalidOperationException(
                $"XAML window registration has invalid WindowType: " +
                $"{windowType?.FullName ?? "(null)"}. Type must be assignable to Window.");
        }

        return (Window)_services.GetRequiredService(windowType);
    }

    /// <summary>
    /// 获取指定前台窗口类型的显示名称。
    /// </summary>
    /// <param name="windowType">窗口类型。</param>
    /// <returns>显示名称；未找到时返回 <see langword="null"/>。</returns>
    public string? GetWindowName(FrontedWindowType windowType)
    {
        return GetWindowName(FrontedWindowHelper.GetFrontedWindowCanonicalId(windowType));
    }

    public string? GetWindowName(string windowId)
    {
        if (_windowRegistry.TryGet(windowId, out var registration))
        {
            var settings = _services.GetService<ISettingsHostService>()?.Settings;
            return FrontedWindowDisplayNameResolver.ResolveDisplayName(
                registration,
                settings?.Language ?? LanguageKey.System,
                settings?.CultureInfo);
        }

        // 未注册时回退查缓存：缓存已使用 OrdinalIgnoreCase，可命中大小写不同的变体。
        _frontedWindows.TryGetValue(windowId, out var window);
        return window?.GetType().Name;
    }

    /// <summary>
    /// 显示所有前台窗口。同步入口：内部以 fire-and-forget 方式调度安全异步流程，
    /// 单窗口失败不会阻止后续窗口打开，也不会向调用方传播异常。
    /// </summary>
    public void AllWindowShow()
    {
        _ = ShowAllWindowsSafelyAsync();
    }

    /// <summary>
    /// 安全地显示所有已注册窗口。单窗口失败被捕获并记录，不阻止后续窗口。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    private async Task ShowAllWindowsSafelyAsync()
    {
        foreach (var registration in _windowRegistry.GetWindows())
        {
            try
            {
                await ShowWindowCoreAsync(registration.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show window {WindowId}", registration.Id);
                // 单窗口失败不阻止后续窗口。
            }
        }
    }

    /// <summary>
    /// 隐藏所有前台窗口。
    /// </summary>
    public void AllWindowHide()
    {
        foreach (var window in _frontedWindows.Where(pair => _frontedWindowStates[pair.Key]))
        {
            window.Value.Hide();
            _frontedWindowStates[window.Key] = false;
            PublishWindowHidden(window.Key);
        }
    }

    /// <summary>
    /// 隐藏指定类型的前台窗口。
    /// </summary>
    /// <param name="windowType">窗口类型。</param>
    /// <remarks>
    /// <see cref="FrontedWindowType.ScoreWindow"/> 是复合操作，会同时隐藏三个比分窗口，
    /// 不会进入普通 Canonical ID 解析（其 Canonical ID 为 <see cref="Guid.Empty"/> 字符串形式，非真实窗口）。
    /// </remarks>
    public void HideWindow(FrontedWindowType windowType)
    {
        // ScoreWindow 是复合操作：同时隐藏三个比分窗口，不进入普通 Canonical ID 解析。
        if (windowType == FrontedWindowType.ScoreWindow)
        {
            HideWindow(FrontedWindowType.ScoreSurWindow);
            HideWindow(FrontedWindowType.ScoreHunWindow);
            HideWindow(FrontedWindowType.ScoreGlobalWindow);
            return;
        }

        HideWindow(FrontedWindowHelper.GetFrontedWindowCanonicalId(windowType));
    }

    public void HideWindow(string windowId)
    {
        // 入口先规范化为 Canonical ID，整条调用链使用规范化后的值。
        var canonicalId = NormalizeWindowId(windowId);

        if (!_frontedWindows.TryGetValue(canonicalId, out var window))
        {
            if (!_windowRegistry.TryGet(canonicalId, out _))
            {
                _ = MessageBoxHelper.ShowErrorAsync(
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "UnregisteredWindowType")}: {canonicalId}",
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "WindowCloseError"));
            }

            return;
        }

        if (!_frontedWindowStates.GetValueOrDefault(canonicalId))
        {
            return;
        }

        window.Hide();
        _frontedWindowStates[canonicalId] = false;
        PublishWindowHidden(canonicalId);
    }

    /// <summary>
    /// 显示指定类型的前台窗口。
    /// </summary>
    /// <param name="windowType">窗口类型。</param>
    /// <remarks>
    /// <see cref="FrontedWindowType.ScoreWindow"/> 是复合操作，会同时显示三个比分窗口，
    /// 不会进入普通 Canonical ID 解析（其 Canonical ID 为 <see cref="Guid.Empty"/> 字符串形式，非真实窗口）。
    /// </remarks>
    public void ShowWindow(FrontedWindowType windowType)
    {
        // ScoreWindow 是复合操作：同时显示三个比分窗口，不进入普通 Canonical ID 解析。
        if (windowType == FrontedWindowType.ScoreWindow)
        {
            ShowWindow(FrontedWindowType.ScoreSurWindow);
            ShowWindow(FrontedWindowType.ScoreHunWindow);
            ShowWindow(FrontedWindowType.ScoreGlobalWindow);
            return;
        }

        ShowWindow(FrontedWindowHelper.GetFrontedWindowCanonicalId(windowType));
    }

    /// <summary>
    /// 显示指定 ID 的前台窗口。同步入口：内部以 fire-and-forget 方式调度安全异步流程，
    /// 窗口显示失败被捕获并提示用户，不会向调用方传播异常。
    /// </summary>
    /// <param name="windowId">窗口 ID（可以是任意大小写变体，内部规范化为 Canonical ID）。</param>
    public void ShowWindow(string windowId)
    {
        _ = ShowWindowSafelyAsync(windowId);
    }

    /// <summary>
    /// 安全地显示指定 ID 的窗口。捕获所有异常并提示用户，不向 SynchronizationContext 传播。
    /// </summary>
    /// <param name="windowId">调用方传入的窗口 ID（可能是任意大小写）。</param>
    /// <returns>表示异步操作的任务。</returns>
    private async Task ShowWindowSafelyAsync(string windowId)
    {
        try
        {
            await ShowWindowCoreAsync(windowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show window {WindowId}", windowId);
            _ = MessageBoxHelper.ShowErrorAsync(
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "WindowLaunchError")}\n{ex.Message}");
        }
    }

    /// <summary>
    /// 实际执行窗口显示的核心逻辑。调用方应通过 <see cref="ShowWindowSafelyAsync"/> 或
    /// <see cref="ShowAllWindowsSafelyAsync"/> 间接调用，避免异常逃逸到 SynchronizationContext。
    /// </summary>
    /// <param name="windowId">调用方传入的窗口 ID（可能是任意大小写）。</param>
    /// <returns>表示异步操作的任务。</returns>
    private async Task ShowWindowCoreAsync(string windowId)
    {
        // 入口先规范化为 Canonical ID，整条调用链使用规范化后的值作为缓存键和事件 payload。
        var canonicalId = NormalizeWindowId(windowId);

        var window = EnsureWindowCreated(canonicalId);
        if (window is null)
        {
            _ = MessageBoxHelper.ShowErrorAsync(
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "UnregisteredWindowType")}: {canonicalId}",
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "WindowLaunchError"));
            _logger.LogError("Unregistered window type {WindowId}", canonicalId);
            return;
        }

        if (_frontedWindowStates.GetValueOrDefault(canonicalId))
        {
            window.Show();
            window.Activate();
            return;
        }

        if (window is FrontedWindowBase frontedWindow)
        {
            await frontedWindow.EnsureInitialWindowSettingsAppliedAsync();
        }

        ApplyWindowLayoutOptions(canonicalId, window);
        window.Show();
        _frontedWindowStates[canonicalId] = true;
        PublishWindowShown(canonicalId);

        if (window is FrontedWindowBase shownFrontedWindow)
        {
            _ = LoadFrontedContentAfterShowAsync(canonicalId, shownFrontedWindow);
        }
    }

    private async Task LoadFrontedContentAfterShowAsync(string windowId, FrontedWindowBase frontedWindow)
    {
        try
        {
            await frontedWindow.LoadOrReloadContentAsync(force: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load v3 fronted window content after show. WindowId: {WindowId}",
                windowId);
        }
    }

    private void ApplyWindowLayoutOptions(string windowId, Window window)
    {
        if (!_windowRegistry.TryGet(windowId, out var registration))
        {
            return;
        }

        if (registration.Kind == FrontedWindowRegistrationKind.V3Layout)
        {
            return;
        }

        if (!File.Exists(_windowLayoutOptionsService.GetUserOptionsPath(registration.Id)))
        {
            return;
        }

        var options = _windowLayoutOptionsService.LoadOptions(registration.Id);
        try
        {
            window.AllowsTransparency = options.AllowTransparency;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(
                ex,
                "Fronted window transparency option could not be applied after source creation. Window: {WindowId}",
                registration.Id);
        }

        if (!TryCreateBackgroundBrush(options.BackgroundColor, out var brush))
        {
            return;
        }

        window.SetCurrentValue(Window.BackgroundProperty, brush);
    }

    /// <summary>
    /// 应用指定窗口的背景色。
    /// </summary>
    /// <param name="fullWindowType">完整窗口类型名。</param>
    /// <returns>成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public async Task<bool> ApplyWindowBackgroundColorAsync(string fullWindowType)
    {
        if (!_windowRegistry.TryGet(fullWindowType, out var registration)
            || !_frontedWindows.TryGetValue(registration.Id, out var window))
        {
            return false;
        }

        var isV3 = registration.Kind == FrontedWindowRegistrationKind.V3Layout;
        var backgroundColor = isV3
            ? (await LoadV3WindowSettingsAsync(registration.Id))?.BackgroundColor
            : _windowLayoutOptionsService.LoadOptions(registration.Id).BackgroundColor;
        if (!TryCreateBackgroundBrush(backgroundColor, out var brush))
        {
            brush = Brushes.Transparent;
        }

        void Apply() => window.SetCurrentValue(Window.BackgroundProperty, brush);

        if (window.Dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            await window.Dispatcher.InvokeAsync(Apply);
        }

        return true;
    }

    /// <summary>
    /// 应用指定窗口的尺寸。
    /// </summary>
    /// <param name="fullWindowType">完整窗口类型名。</param>
    /// <returns>成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public async Task<bool> ApplyWindowSizeAsync(string fullWindowType)
    {
        if (!_windowRegistry.TryGet(fullWindowType, out var registration)
            || !_frontedWindows.TryGetValue(registration.Id, out var window))
        {
            return false;
        }

        var isV3 = registration.Kind == FrontedWindowRegistrationKind.V3Layout;
        var v3Settings = isV3
            ? await LoadV3WindowSettingsAsync(registration.Id)
            : null;
        var options = isV3
            ? null
            : _windowLayoutOptionsService.LoadOptions(registration.Id);
        var width = v3Settings?.WindowWidth ?? options?.WindowWidth;
        var height = v3Settings?.WindowHeight ?? options?.WindowHeight;
        if (width is null && height is null)
        {
            return false;
        }

        void Apply()
        {
            if (width is { } w && w > 0 && double.IsFinite(w))
            {
                window.Width = w;
            }

            if (height is { } h && h > 0 && double.IsFinite(h))
            {
                window.Height = h;
            }
        }

        if (window.Dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            await window.Dispatcher.InvokeAsync(Apply);
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RestartWindowForTransparencyChangeAsync(string fullWindowType)
    {
        if (!_windowRegistry.TryGet(fullWindowType, out var registration)
            || !_frontedWindows.TryGetValue(registration.Id, out var oldWindow))
        {
            return false;
        }

        // 静默重建只支持宿主创建的 v3 Window。
        // XAML 窗口在 DI 中注册为 singleton，Close() 后 DI 仍返回同一已关闭实例，
        // WPF Window 关闭后无法再次 Show，因此必须直接拒绝，避免破坏窗口状态。
        if (registration is not FrontedV3LayoutWindowRegistration)
        {
            return false;
        }

        if (oldWindow.Dispatcher.CheckAccess())
        {
            return await RestartWindowForTransparencyChangeOnDispatcherAsync(registration, oldWindow);
        }

        return await oldWindow.Dispatcher
            .InvokeAsync(() => RestartWindowForTransparencyChangeOnDispatcherAsync(registration, oldWindow))
            .Task
            .Unwrap();
    }

    private async Task<bool> RestartWindowForTransparencyChangeOnDispatcherAsync(
        FrontedWindowRegistration registration,
        Window oldWindow)
    {
        var windowId = registration.Id;
        if (!_frontedWindows.TryGetValue(windowId, out var currentWindow)
            || !ReferenceEquals(currentWindow, oldWindow))
        {
            return false;
        }

        var wasShown = _frontedWindowStates.GetValueOrDefault(windowId);

        _frontedWindows.Remove(windowId);
        _frontedWindowStates.Remove(windowId);

        if (wasShown)
        {
            PublishWindowHidden(windowId);
        }

        CloseFrontedWindowInstance(oldWindow);

        if (!wasShown)
        {
            return true;
        }

        var newWindow = EnsureWindowCreated(windowId);
        if (newWindow is null)
        {
            _logger.LogWarning(
                "Failed to recreate fronted window after transparency change. WindowId: {WindowId}",
                windowId);
            return true;
        }

        if (newWindow is FrontedWindowBase frontedWindow)
        {
            await frontedWindow.EnsureInitialWindowSettingsAppliedAsync();
        }

        ApplyWindowLayoutOptions(windowId, newWindow);
        newWindow.Show();
        _frontedWindowStates[windowId] = true;
        PublishWindowShown(windowId);

        if (newWindow is FrontedWindowBase shownFrontedWindow)
        {
            _ = LoadFrontedContentAfterShowAsync(windowId, shownFrontedWindow);
        }

        return true;
    }

    private static void CloseFrontedWindowInstance(Window window)
    {
        if (window is FrontedWindowBase frontedWindow)
        {
            frontedWindow.RequestServiceClose();
            return;
        }

        window.Close();
    }

    private async Task<FrontedWindowSettings?> LoadV3WindowSettingsAsync(string fullWindowType)
    {
        try
        {
            var config = await _services.GetRequiredService<IFrontedLayoutService>()
                .LoadWindowConfigAsync(fullWindowType);
            return config?.WindowSettings;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load v3 fronted window settings. Window: {FullWindowType}", fullWindowType);
            return null;
        }
    }

    public (double Width, double Height)? GetWindowSize(string fullWindowType)
    {
        if (!_windowRegistry.TryGet(fullWindowType, out var registration)
            || !_frontedWindows.TryGetValue(registration.Id, out var window))
        {
            return null;
        }

        var width = window.Width;
        var height = window.Height;

        if (double.IsNaN(width) || double.IsNaN(height)
            || width <= 0 || height <= 0)
        {
            return null;
        }

        return (width, height);
    }

    private static bool TryCreateBackgroundBrush(string? colorText, out Brush brush)
    {
        brush = Brushes.Transparent;
        if (string.IsNullOrWhiteSpace(colorText))
        {
            return false;
        }

        try
        {
            brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorText)!);
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public async Task ReloadFrontedLayoutsAsync()
    {
        _services.GetService<IFrontedResourceResolver>()?.ClearCache();

        foreach (var pair in _frontedWindows.ToArray())
        {
            if (pair.Value is not FrontedWindowBase frontedWindow
                || !_windowRegistry.TryGet(pair.Key, out var registration)
                || registration.Kind != FrontedWindowRegistrationKind.V3Layout)
            {
                continue;
            }

            try
            {
                var requestedTransparency = await frontedWindow.GetRequestedAllowsTransparencyAsync();
                if (requestedTransparency.HasValue
                    && requestedTransparency.Value != frontedWindow.AllowsTransparency)
                {
                    await RestartWindowForTransparencyChangeAsync(registration.Id);
                    continue;
                }

                await frontedWindow.ReloadFrontedLayoutAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reload fronted v3 layout for {WindowId}.", registration.Id);
            }
        }
    }

    /// <inheritdoc/>
    public void MarkWindowLayoutDirty(string windowIdOrFullWindowType)
    {
        if (string.IsNullOrWhiteSpace(windowIdOrFullWindowType))
        {
            return;
        }

        // 规范化为 Canonical ID，保证与缓存键一致。
        var windowId = NormalizeWindowId(windowIdOrFullWindowType);

        if (_frontedWindows.TryGetValue(windowId, out var window)
            && window is FrontedWindowBase frontedWindow)
        {
            frontedWindow.MarkLayoutDirty();
        }
    }

    private void PublishWindowShown(string windowId)
    {
        try
        {
            _eventBus?.Publish(new FrontedBehaviorEvent
            {
                EventType = "WindowShown",
                WindowId = windowId,
                Source = "WindowLifecycle",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish WindowShown event for {WindowId}.", windowId);
        }
    }

    private void PublishWindowHidden(string windowId)
    {
        try
        {
            _eventBus?.Publish(new FrontedBehaviorEvent
            {
                EventType = "WindowHidden",
                WindowId = windowId,
                Source = "WindowLifecycle",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish WindowHidden event for {WindowId}.", windowId);
        }
    }

}
