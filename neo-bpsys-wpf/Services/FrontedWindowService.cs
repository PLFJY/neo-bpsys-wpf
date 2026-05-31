using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.AttachedBehaviors;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Windows;
using System.IO;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static neo_bpsys_wpf.Core.Helpers.FrontedWindowHelper;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 前台窗口服务, 实现了 <see cref="IFrontedWindowService"/> 接口，负责与前台窗口进行交互
/// </summary>
public class FrontedWindowService : IFrontedWindowService
{
    /// <summary>
    /// 前台窗口列表
    /// </summary>
    public Dictionary<string, Window> FrontedWindows { get; private set; } = [];

    /// <summary>
    /// 前台窗口状态列表
    /// </summary>
    public Dictionary<string, bool> FrontedWindowStates { get; private set; } = [];

    /// <summary>
    /// 前台画布列表
    /// </summary>
    public List<(string, string)> FrontedCanvas { get; private set; } = []; // 窗口ID, 画布名称

    /// <summary>
    /// 外部控件默认位置列表
    /// </summary>
    private readonly Dictionary<FrameworkElement, ElementInfo> _externalControlDefaultPosition = [];

    private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
    private readonly ISettingsHostService _settingsHostService;
    private readonly ILogger<FrontedWindowService> _logger;
    private Settings? _windowSizeSettings;

    public FrontedWindowService(
        BpWindow bpWindow,
        CutSceneWindow cutSceneWindow,
        GameDataWindow gameDataWindow,
        ScoreSurWindow scoreSurWindow,
        ScoreHunWindow scoreHunWindow,
        ScoreGlobalWindow scoreGlobalWindow,
        WidgetsWindow widgetsWindow,
        ISharedDataService sharedDataService,
        ISettingsHostService settingsHostService,
        ILogger<FrontedWindowService> logger
    )
    {
        _settingsHostService = settingsHostService;
        _logger = logger;
        if (!Directory.Exists(AppConstants.AppDataPath)) Directory.CreateDirectory(AppConstants.AppDataPath);

        // 注册窗口和画布
        RegisterFrontedWindowAndCanvas();

        //加载后期注入的控件
        LoadInjectedControl();

#if DEBUG
        //记录初始位置 (仅DEBUG生效)
        foreach (var i in FrontedCanvas)
        {
            RecordInitialPositions(i.Item1, i.Item2, true);
        }
#endif

        //从文件加载位置信息
        _ = LoadElementsPositionOnStartup();

        //AttachWindowSizeHandlers(_settingsHostService.Settings);
        //_settingsHostService.SettingsChanged += (_, settings) => AttachWindowSizeHandlers(settings);
    }

    /// <summary>
    /// 加载后期注入的控件
    /// </summary>
    private void LoadInjectedControl()
    {
        foreach (var info in FrontedWindowRegistryService.InjectedControls)
        {
            InjectControl(info.TargetWindow, info.TargetCanvas, info.Control, info.DefaultInfo);
        }
    }

    /// <summary>
    /// 从文件加载位置信息
    /// </summary>
    /// <returns></returns>
    private async Task LoadElementsPositionOnStartup()
    {
        foreach (var i in FrontedCanvas)
        {
            await LoadWindowElementsPositionOnStartupAsync(i.Item1, i.Item2);
        }
    }

    /// <summary>
    /// 注册窗口和画布
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="window">窗口</param>
    /// <param name="canvasNames">画布名称</param>
    public void RegisterFrontedWindowAndCanvas(string windowId, Window window, string[]? canvasNames = null)
    {
        canvasNames ??= ["BaseCanvas"];

        if (FrontedWindows.TryAdd(windowId, window))
        {
            FrontedWindowStates[windowId] = false;
        }

        foreach (var canvasName in canvasNames)
        {
            if (!FrontedCanvas.Contains((windowId, canvasName)))
                FrontedCanvas.Add((windowId, canvasName));
        }
    }

    private void RegisterFrontedWindowAndCanvas()
    {
        var windowInfos = FrontedWindowRegistryService.RegisteredWindow;

        foreach (var info in windowInfos)
        {
            if (info.WindowType != null)
                RegisterFrontedWindowAndCanvas(info.Id,
                    IAppHost.Host?.Services.GetRequiredService(info.WindowType) as Window ??
                    throw new InvalidOperationException(),
                    info.Canvas.Select(x => x.Name).ToArray());
        }
    }


    public string? GetWindowName(FrontedWindowType windowType)
    {
        return GetWindowName(GetFrontedWindowGuid(windowType));
    }

    public string? GetWindowName(string windowId)
    {
        FrontedWindows.TryGetValue(windowId, out var window);
        return window?.GetType().Name;
    }

    public FrameworkElement GetInjectedControl(string guid)
    {
        var control = FrontedWindowRegistryService.InjectedControls
            .First(x => x.Id == guid).Control;

        return control;
    }

    //private void AttachWindowSizeHandlers(Settings settings)
    //{
    //    if (_windowSizeSettings != null)
    //    {
    //        _windowSizeSettings.BpWindowSettings.PropertyChanged -= OnBpWindowSettingsChanged;
    //        _windowSizeSettings.CutSceneWindowSettings.PropertyChanged -= OnCutSceneWindowSettingsChanged;
    //        _windowSizeSettings.GameDataWindowSettings.PropertyChanged -= OnGameDataWindowSettingsChanged;
    //        _windowSizeSettings.WidgetsWindowSettings.PropertyChanged -= OnWidgetsWindowSettingsChanged;
    //        _windowSizeSettings.ScoreWindowSettings.PropertyChanged -= OnScoreWindowSettingsChanged;
    //    }

    //    _windowSizeSettings = settings;
    //    settings.BpWindowSettings.PropertyChanged += OnBpWindowSettingsChanged;
    //    settings.CutSceneWindowSettings.PropertyChanged += OnCutSceneWindowSettingsChanged;
    //    settings.GameDataWindowSettings.PropertyChanged += OnGameDataWindowSettingsChanged;
    //    settings.WidgetsWindowSettings.PropertyChanged += OnWidgetsWindowSettingsChanged;
    //    settings.ScoreWindowSettings.PropertyChanged += OnScoreWindowSettingsChanged;

    //    ApplyWindowSizes(settings);
    //}

    //private void ApplyWindowSizes(Settings settings)
    //{
    //    UpdateWindowSize(FrontedWindowType.BpWindow, settings.BpWindowSettings.WindowSize);
    //    UpdateWindowSize(FrontedWindowType.CutSceneWindow, settings.CutSceneWindowSettings.WindowSize);
    //    UpdateWindowSize(FrontedWindowType.GameDataWindow, settings.GameDataWindowSettings.WindowSize);
    //    UpdateWindowSize(FrontedWindowType.WidgetsWindow, settings.WidgetsWindowSettings.WindowSize);
    //    UpdateWindowSize(FrontedWindowType.ScoreGlobalWindow, settings.ScoreWindowSettings.ScoreGlobalWindowSize);
    //    UpdateWindowSize(FrontedWindowType.ScoreSurWindow, settings.ScoreWindowSettings.ScoreInGameWindowSize);
    //    UpdateWindowSize(FrontedWindowType.ScoreHunWindow, settings.ScoreWindowSettings.ScoreInGameWindowSize);
    //}

    //private void OnBpWindowSettingsChanged(object? sender, PropertyChangedEventArgs e)
    //{
    //    if (e.PropertyName == nameof(BpWindowSettings.WindowSize))
    //        UpdateWindowSize(FrontedWindowType.BpWindow, _settingsHostService.Settings.BpWindowSettings.WindowSize);
    //}

    //private void OnCutSceneWindowSettingsChanged(object? sender, PropertyChangedEventArgs e)
    //{
    //    if (e.PropertyName == nameof(CutSceneWindowSettings.WindowSize))
    //        UpdateWindowSize(FrontedWindowType.CutSceneWindow, _settingsHostService.Settings.CutSceneWindowSettings.WindowSize);
    //}

    //private void OnGameDataWindowSettingsChanged(object? sender, PropertyChangedEventArgs e)
    //{
    //    if (e.PropertyName == nameof(GameDataWindowSettings.WindowSize))
    //        UpdateWindowSize(FrontedWindowType.GameDataWindow, _settingsHostService.Settings.GameDataWindowSettings.WindowSize);
    //}

    //private void OnWidgetsWindowSettingsChanged(object? sender, PropertyChangedEventArgs e)
    //{
    //    if (e.PropertyName == nameof(WidgetsWindowSettings.WindowSize))
    //        UpdateWindowSize(FrontedWindowType.WidgetsWindow, _settingsHostService.Settings.WidgetsWindowSettings.WindowSize);
    //}

    //private void OnScoreWindowSettingsChanged(object? sender, PropertyChangedEventArgs e)
    //{
    //    if (e.PropertyName == nameof(ScoreWindowSettings.ScoreInGameWindowSize))
    //    {
    //        var size = _settingsHostService.Settings.ScoreWindowSettings.ScoreInGameWindowSize;
    //        UpdateWindowSize(FrontedWindowType.ScoreSurWindow, size);
    //        UpdateWindowSize(FrontedWindowType.ScoreHunWindow, size);
    //    }

    //    if (e.PropertyName == nameof(ScoreWindowSettings.ScoreGlobalWindowSize))
    //        UpdateWindowSize(FrontedWindowType.ScoreGlobalWindow, _settingsHostService.Settings.ScoreWindowSettings.ScoreGlobalWindowSize);
    //}

    //private void UpdateWindowSize(FrontedWindowType windowType, Size size)
    //{
    //    if (!FrontedWindows.TryGetValue(GetFrontedWindowGuid(windowType), out var window))
    //        return;

    //    void ApplySize()
    //    {
    //        window.Width = size.Width;
    //        window.Height = size.Height;
    //    }

    //    if (window.Dispatcher.CheckAccess())
    //        ApplySize();
    //    else
    //        window.Dispatcher.Invoke(ApplySize);
    //}

    /// <summary>
    /// 注入控件
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="canvasName">画布名称</param>
    /// <param name="control">控件</param>
    /// <param name="defaultInfo">默认位置信息</param>
    public void InjectControl(string windowId, string canvasName, FrameworkElement control, ElementInfo defaultInfo)
    {
        if (FrontedWindows.TryGetValue(windowId, out var window))
        {
            var canvas = window.FindName(canvasName) as Canvas;
            if (defaultInfo.Width != null)
                control.Width = (double)defaultInfo.Width;
            if (defaultInfo.Height != null)
                control.Height = (double)defaultInfo.Height;
            if (defaultInfo.Top != null)
                Canvas.SetTop(control, (double)defaultInfo.Top);
            if (defaultInfo.Left != null)
                Canvas.SetLeft(control, (double)defaultInfo.Left);
            _externalControlDefaultPosition[control] = defaultInfo;

            var designerModeBinding = new Binding("IsDesignerMode")
            {
                Source = canvas?.DataContext,
            };
            BindingOperations.SetBinding(control, DesignBehavior.IsDesignerModeProperty, designerModeBinding);

            canvas?.Children.Add(control);
        }
        else
        {
            _logger.LogError("Window {WindowId} not found.", windowId);
        }
    }

    #region 窗口显示/隐藏管理

    public void AllWindowShow()
    {
        foreach (var window in FrontedWindows.Where(pair => !FrontedWindowStates[pair.Key]))
        {
            window.Value.Show();
            FrontedWindowStates[window.Key] = true;
        }
    }

    public void AllWindowHide()
    {
        foreach (var window in FrontedWindows.Where(pair => FrontedWindowStates[pair.Key]))
        {
            window.Value.Hide();
            FrontedWindowStates[window.Key] = false;
        }
    }

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    /// <param name="windowType"></param>
    public void HideWindow(FrontedWindowType windowType)
    {
        HideWindow(GetFrontedWindowGuid(windowType));
    }

    /// <inheritdoc/>
    public void HideWindow(string windowId)
    {
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("UnregisteredWindowType")}: {windowId}", I18nHelper.GetLocalizedString("WindowCloseError"));
            return;
        }

        if (!FrontedWindowStates[windowId]) return;
        window.Hide();
        FrontedWindowStates[windowId] = false;
    }

    /// <summary>
    /// 显示窗口
    /// </summary>
    /// <param name="windowType"></param>
    public void ShowWindow(FrontedWindowType windowType)
    {
        ShowWindow(GetFrontedWindowGuid(windowType));
    }

    /// <inheritdoc/>
    public void ShowWindow(string windowId)
    {
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("UnregisteredWindowType")}: {windowId}", I18nHelper.GetLocalizedString("WindowLaunchError"));
            _logger.LogError("Unregistered window type{WindowId}", windowId);
            return;
        }

        if (FrontedWindowStates[windowId])
        {
            window.Activate();
            return;
        }
        else
        {
            window.Show();
            FrontedWindowStates[windowId] = true;
        }
    }

    public async Task ReloadFrontedLayoutsAsync()
    {
        foreach (var window in FrontedWindows.Values)
        {
            var method = window.GetType().GetMethod("ReloadFrontedLayoutAsync");
            if (method is null)
            {
                continue;
            }

            try
            {
                if (method.Invoke(window, null) is Task task)
                {
                    await task;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reload fronted v3 layout for {WindowType}.", window.GetType().Name);
            }
        }
    }

    #endregion

    #region 设计者模式

    /// <summary>
    /// 记录窗口中元素的初始位置 (仅在DEBUG下有效)
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="canvasName">画布名称</param>
    private void RecordInitialPositions(string windowId, string canvasName = "BaseCanvas", bool isInitial = false)
    {
        if (!FrontedWindows.TryGetValue(windowId, out var window)) return;
        var path = Path.Combine(AppConstants.AppDataPath, $"{window.GetType().Name}Config-{canvasName}.default.json");

        if (File.Exists(path)) return;

        var positions = GetElementsPositions(window, canvasName, isInitial);
        if (positions == null) return;
        var output = JsonSerializer.Serialize(positions, _jsonSerializerOptions);
        try
        {
            File.WriteAllText(path, output);
        }
        catch (Exception ex)
        {
            _ = MessageBoxHelper.ShowErrorAsync(ex.Message, I18nHelper.GetLocalizedString("ErrorWhenGeneratingFrontendConfigurationFile"));
        }
    }

    /// <summary>
    /// 获取窗口中元素的位置信息
    /// </summary>
    /// <param name="window"></param>
    /// <param name="canvasName"></param>
    /// <returns></returns>
    private Dictionary<string, ElementInfo>? GetElementsPositions(Window window, string canvasName,
        bool isInitial = false)
    {
        if (window.FindName(canvasName) is not Canvas canvas)
            return null;

        var positions = new Dictionary<string, ElementInfo>();
        foreach (UIElement child in canvas.Children)
        {
            if (child is not FrameworkElement fe || string.IsNullOrEmpty(fe.Name)) continue;
            if (fe.Tag?.ToString() == "nv") continue;

            if (isInitial && _externalControlDefaultPosition.ContainsKey(fe))
            {
                continue;
            }

            positions[fe.Name] = new ElementInfo(
                double.IsNaN(fe.Width) ? null : fe.Width,
                double.IsNaN(fe.Height) ? null : fe.Height,
                double.IsNaN(Canvas.GetLeft(fe)) ? 0 : Canvas.GetLeft(fe),
                double.IsNaN(Canvas.GetTop(fe)) ? 0 : Canvas.GetTop(fe));
        }

        return positions;
    }


    /// <summary>
    /// 保存指定窗口的指定Canvas中元素的位置信息
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="canvasName">画布名称</param>
    public void SaveWindowCanvasElementsPosition(FrontedWindowType windowType, string canvasName = "BaseCanvas")
    {
        SaveWindowCanvasElementsPosition(GetFrontedWindowGuid(windowType), canvasName);
    }

    public void SaveWindowCanvasElementsPosition(string windowId, string canvasName = "BaseCanvas")
    {
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("UnregisteredWindowType")}: {windowId}",
                I18nHelper.GetLocalizedString("ConfigurationFileSaveError"));
            return;
        }

        var positions = GetElementsPositions(window, canvasName);
        if (positions == null) return;

        var path = Path.Combine(AppConstants.AppDataPath, $"{window.GetType().Name}Config-{canvasName}.json");
        try
        {
            var jsonContent = JsonSerializer.Serialize(positions, _jsonSerializerOptions);
            File.WriteAllText(path, jsonContent);
        }
        catch (Exception ex)
        {
            _ = MessageBoxHelper.ShowInfoAsync(
                $"{I18nHelper.GetLocalizedString("SaveFrontendConfigurationFileFailed")}\n{ex.Message}",
                I18nHelper.GetLocalizedString("SaveInfo"));
        }
    }

    /// <summary>
    /// 保存指定窗口的元素位置信息
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    public void SaveWindowElementsPosition(FrontedWindowType windowType)
    {
        SaveWindowElementsPosition(GetFrontedWindowGuid(windowType));
    }

    public void SaveWindowElementsPosition(string windowId)
    {
        if (windowId == GetFrontedWindowGuid(FrontedWindowType.ScoreWindow))
        {
            SaveWindowElementsPosition(FrontedWindowType.ScoreSurWindow);
            SaveWindowElementsPosition(FrontedWindowType.ScoreHunWindow);
            SaveWindowElementsPosition(FrontedWindowType.ScoreGlobalWindow);
        }

        foreach (var tuple in FrontedCanvas.Where(x =>
                     x.Item1 == windowId))
        {
            SaveWindowCanvasElementsPosition(tuple.Item1, tuple.Item2);
        }
    }


    /// <summary>
    /// 批量保存所有窗口中元素位置信息
    /// </summary>
    public void SaveAllWindowElementsPosition()
    {
        foreach (var i in FrontedCanvas)
        {
            SaveWindowCanvasElementsPosition(i.Item1, i.Item2);
        }
    }

    /// <summary>
    /// 程序启动时从JSON中加载窗口中元素的位置信息
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="canvasName">画布名称</param>
    private async Task LoadWindowElementsPositionOnStartupAsync(string windowId,
        string canvasName = "BaseCanvas")
    {
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            await MessageBoxHelper.ShowErrorAsync(
                $"{I18nHelper.GetLocalizedString("UnregisteredWindowType")}: {windowId}",
                I18nHelper.GetLocalizedString("ConfigurationFileLoadingError"));
            return;
        }

        var path = Path.Combine(AppConstants.AppDataPath, $"{window.GetType().Name}Config-{canvasName}.json");
        if (!File.Exists(path)) return;

        try
        {
            var jsonContent = await File.ReadAllTextAsync(path);
            LoadElementsPositions(canvasName, jsonContent, window);
        }
        catch (Exception ex)
        {
            File.Move(path, $"{path}.disabled", true);
            await MessageBoxHelper.ShowErrorAsync(ex.Message);
        }
    }

    /// <summary>
    /// 从JSON中加载窗口中元素位置信息
    /// </summary>
    /// <param name="canvasName">画布名称</param>
    /// <param name="jsonContent">JSON内容</param>
    /// <param name="window">窗口实例</param>
    private void LoadElementsPositions(string canvasName, string jsonContent, Window window)
    {
        var positions = JsonSerializer.Deserialize<Dictionary<string, ElementInfo>>(jsonContent);

        if (window.FindName(canvasName) is not Canvas canvas || positions == null) return;
        foreach (UIElement child in canvas.Children)
        {
            if (child is not FrameworkElement fe) continue;
            if (fe.Tag?.ToString() == "nv") continue;

            if (positions.TryGetValue(fe.Name, out var value))
            {
                if (value.Width != null)
                    fe.Width = (double)value.Width;
                if (value.Height != null)
                    fe.Height = (double)value.Height;
                if (value.Left != null)
                    Canvas.SetLeft(fe, (double)value.Left);
                if (value.Top != null)
                    Canvas.SetTop(fe, (double)value.Top);
            }
            else if (_externalControlDefaultPosition.TryGetValue(fe, out var info))
            {
                if (info.Width != null)
                    fe.Width = (double)info.Width;
                if (info.Height != null)
                    fe.Height = (double)info.Height;
                if (info.Left != null)
                    Canvas.SetLeft(fe, (double)info.Left);
                if (info.Top != null)
                    Canvas.SetTop(fe, (double)info.Top);
            }
        }
    }

    /// <summary>
    /// 还原窗口中的元素到初始位置
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="canvasName">画布名称</param>
    public async Task RestoreInitialPositions(FrontedWindowType windowType, string canvasName = "BaseCanvas")
    {
        await RestoreInitialPositions(GetFrontedWindowGuid(windowType), canvasName);
    }

    public async Task RestoreInitialPositions(string windowId, string canvasName = "BaseCanvas")
    {
        if (!FrontedWindows.TryGetValue(windowId, out var window))
        {
            await MessageBoxHelper.ShowErrorAsync(
                $"{I18nHelper.GetLocalizedString("UnregisteredWindowType")}: {windowId}",
                I18nHelper.GetLocalizedString("FrontendDefaultConfigurationRestoreError"));
            return;
        }

        if (!await MessageBoxHelper.ShowConfirmAsync($"{I18nHelper.GetLocalizedString("ConfigurationResetConfirmation")}: {window.GetType().Name}-{canvasName}",
            I18nHelper.GetLocalizedString("ResetTip"), I18nHelper.GetLocalizedString("Confirm"), I18nHelper.GetLocalizedString("Cancel")))
            return;


        // Built in fronted window
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "Resources", "FrontedDefaultPositions", $"{window.GetType().Name}Config-{canvasName}.default.json");

        if (!File.Exists(path))
        {
            // Find Plugin Path
            var type = window.GetType();
            if (PluginService.FrontedWindowAssemblyFolder
                .TryGetValue(type, out var folderPath))
            {
                path = Path.Combine(folderPath, "FrontedDefaultPositions",
                    $"{type.Name}Config-{canvasName}.default.json");
            }
            else
            {
                await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("UnknownWindowSource"));
                return;
            }
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            LoadElementsPositions(canvasName, json, window);

            var customFilePath =
                Path.Combine(AppConstants.AppDataPath, $"{window.GetType().Name}Config-{canvasName}.json");

            if (File.Exists(customFilePath))
                File.Move(customFilePath, $"{customFilePath}.disabled", true);

            if (File.Exists(path) && Directory.Exists(AppConstants.AppDataPath))
                File.Copy(path, customFilePath, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reading frontend default configuration error");
            await MessageBoxHelper.ShowErrorAsync(
                $"{I18nHelper.GetLocalizedString("ReadFrontendDefaultConfigurationError")}\n{I18nHelper.GetLocalizedString("CannotFindDefaultLayoutConfigurationFromWindowProvider")}",
                I18nHelper.GetLocalizedString("ReadFrontendDefaultConfigurationError"));
        }
    }

    #endregion

    #region 分数统计

    [Obsolete("全局比分状态由 CurrentGame.MatchScore 驱动。请通过 IMatchScoreService 修改比分。")]
    public void SetGlobalScore(TeamType team, GameProgress gameProgress, Camp camp, int score)
    {
        // Compatibility adapter: ScoreGlobalWindow is rendered from CurrentGame.MatchScore by v3 controls.
    }

    [Obsolete("全局比分状态由 CurrentGame.MatchScore 驱动。请通过 IMatchScoreService 修改比分。")]
    public void SetGlobalScoreToBar(TeamType team, GameProgress gameProgress)
    {
        // Compatibility adapter: empty half display is derived from CurrentGame.MatchScore by v3 controls.
    }

    [Obsolete("全局比分状态由 CurrentGame.MatchScore 驱动。请通过 IMatchScoreService 修改比分。")]
    public void ResetGlobalScore()
    {
        // Compatibility adapter: callers should clear CurrentGame.MatchScore through IMatchScoreService.
    }

    #endregion

    #region 动画

    /// <summary>
    /// 渐显动画
    /// </summary>
    /// <param name="windowType">窗体类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    [Obsolete("请使用 IAnimationService.PlayPickFadeIn 替代。此方法将在 3.0.0 中移除。")]
    public void FadeInAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        FadeInAnimation(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.PlayPickFadeIn 替代。此方法将在 3.0.0 中移除。")]
    public void FadeInAnimation(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window)) return;

        if (window.FindName(ctrName) is FrameworkElement element)
        {
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5))));
        }
    }

    /// <summary>
    /// 渐隐动画
    /// </summary>
    /// <param name="windowType">窗体类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    [Obsolete("请使用 IAnimationService.PlayPickFadeOut 替代。此方法将在未来版本中移除。")]
    public void FadeOutAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        FadeOutAnimation(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.PlayPickFadeOut 替代。此方法将在未来版本中移除。")]
    public void FadeOutAnimation(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window)) return;
        if (window.FindName(ctrName) is FrameworkElement element)
        {
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5))));
        }
    }

    /// <summary>
    /// 呼吸动画开始
    /// </summary>
    /// <param name="windowType">窗体类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    [Obsolete("请使用 IAnimationService.StartPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStart(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        await BreathingStart(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.StartPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStart(string windowId, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window)) return;
        if (window.FindName(ctrName) is not FrameworkElement element) return;

        element.Opacity = 0;
        element.Visibility = Visibility.Visible;
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.25))));
        await Task.Delay(250);

        // 如果已有动画，先停止
        await BreathingStop(windowId, controlNameHeader, controlIndex, controlNameFooter);

        var animation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.25,
            Duration = TimeSpan.FromSeconds(1),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin(element);
        element.Tag = storyboard; // 用于后续停止动画
    }

    /// <summary>
    /// 停止呼吸动画
    /// </summary>
    /// <param name="windowType">窗体类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    [Obsolete("请使用 IAnimationService.StopPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStop(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        await BreathingStop(GetFrontedWindowGuid(windowType), controlNameHeader, controlIndex, controlNameFooter);
    }

    [Obsolete("请使用 IAnimationService.StopPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    public async Task BreathingStop(string windowId, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontedWindows.TryGetValue(windowId, out var window)) return;
        if (window.FindName(ctrName) is not FrameworkElement element) return;
        if (element.Tag is not Storyboard storyboard) return;

        storyboard.Stop();
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.25))));
        await Task.Delay(250);

        element.Opacity = 0; // 恢复初始状态
        element.Tag = null;
        element.Visibility = Visibility.Hidden;
    }

    #endregion
}
