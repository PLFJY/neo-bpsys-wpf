using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Controls;
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
    /// 全局分数控件列表
    /// </summary>
    private readonly Dictionary<GameProgress, FrameworkElement> _mainGlobalScoreControls = [];

    /// <summary>
    /// 客队分数控件列表
    /// </summary>
    private readonly Dictionary<GameProgress, FrameworkElement> _awayGlobalScoreControls = [];

    /// <summary>
    /// 外部控件默认位置列表
    /// </summary>
    private readonly Dictionary<FrameworkElement, ElementInfo> _externalControlDefaultPosition = [];

    private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;
    private readonly ILogger<FrontedWindowService> _logger;

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
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        _logger = logger;
        if (!Directory.Exists(AppConstants.AppDataPath)) Directory.CreateDirectory(AppConstants.AppDataPath);

        // 注册窗口和画布
        RegisterFrontedWindowAndCanvas();

        //注册分数统计界面的分数控件
        GlobalScoreControlsReg();

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

        //分数统计部分的消息订阅和部分参数
        _isBo3Mode = sharedDataService.IsBo3Mode;
        _globalScoreTotalMargin = sharedDataService.GlobalScoreTotalMargin;
        sharedDataService.GlobalScoreTotalMarginChanged += OnGlobalScoreTotalMarginChanged;
        sharedDataService.IsBo3ModeChanged += OnBo3ModeChanged;
        OnBo3ModeChanged(this, EventArgs.Empty);
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

        Task.Delay(250);
        Application.Current.MainWindow?.Activate();
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

        Task.Delay(250);
        Application.Current.MainWindow?.Activate();
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

        if (windowId == GetFrontedWindowGuid(FrontedWindowType.ScoreGlobalWindow) &&
            canvasName == "ScoreGlobalCanvas" && _isBo3Mode) return;

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

    private bool _isBo3Mode;

    /// <summary>
    /// 注册全局计分板控件
    /// </summary>
    private void GlobalScoreControlsReg()
    {
        if (FrontedWindows[GetFrontedWindowGuid(FrontedWindowType.ScoreGlobalWindow)].FindName("BaseCanvas") is not
            Canvas canvas) return;
        //主队
        foreach (var progress in Enum.GetValues<GameProgress>())
        {
            if (progress == GameProgress.Free) continue;
            var control = new GlobalScorePresenter();
            RegisterScoreGlobalControl(nameof(TeamType.HomeTeam), progress, _mainGlobalScoreControls, control);
        }

        //客队
        foreach (var progress in Enum.GetValues<GameProgress>())
        {
            if (progress == GameProgress.Free) continue;
            var control = new GlobalScorePresenter();
            RegisterScoreGlobalControl(nameof(TeamType.AwayTeam), progress, _awayGlobalScoreControls, control);
        }

        //添加控件到 Canvas 并设置位置
        foreach (var item in _mainGlobalScoreControls)
        {
            AddScoreGlobalControlToCanvas(item.Value, canvas, item.Key, 93);
            SetBinding(item.Value, TextBlock.FontSizeProperty, "Settings.TextSettings.ScoreGlobal_Data.FontSize");
            SetBinding(item.Value, TextBlock.FontFamilyProperty, "Settings.TextSettings.ScoreGlobal_Data.FontFamily");
            SetBinding(item.Value, TextBlock.FontWeightProperty, "Settings.TextSettings.ScoreGlobal_Data.FontWeight");
            SetBinding(item.Value, TextBlock.ForegroundProperty, "Settings.TextSettings.ScoreGlobal_Data.Foreground");
        }

        foreach (var item in _awayGlobalScoreControls)
        {
            AddScoreGlobalControlToCanvas(item.Value, canvas, item.Key, 150);
            SetBinding(item.Value, TextBlock.FontSizeProperty, "Settings.TextSettings.ScoreGlobal_Data.FontSize");
            SetBinding(item.Value, TextBlock.FontFamilyProperty, "Settings.TextSettings.ScoreGlobal_Data.FontFamily");
            SetBinding(item.Value, TextBlock.FontWeightProperty, "Settings.TextSettings.ScoreGlobal_Data.FontWeight");
            SetBinding(item.Value, TextBlock.ForegroundProperty, "Settings.TextSettings.ScoreGlobal_Data.Foreground");
        }

        return;

        void SetBinding(UIElement textBlock, DependencyProperty dependencyProperty, string bindingPath)
        {
            BindingOperations.SetBinding(textBlock, dependencyProperty, new Binding(bindingPath)
            {
                Source = canvas.DataContext
            });
        }
    }

    /// <summary>
    /// 设置分数统计
    /// </summary>
    /// <param name="team"></param>
    /// <param name="gameProgress"></param>
    /// <param name="camp"></param>
    /// <param name="score"></param>
    public void SetGlobalScore(TeamType team, GameProgress gameProgress, Camp camp, int score)
    {
        GlobalScorePresenter presenter = new();

        if (team == TeamType.HomeTeam)
        {
            if (_mainGlobalScoreControls[gameProgress] is GlobalScorePresenter item)
                presenter = item;
        }
        else
        {
            if (_awayGlobalScoreControls[gameProgress] is GlobalScorePresenter item1)
                presenter = item1;
        }

        presenter.IsCampVisible = true;
        presenter.IsHunIcon = camp == Camp.Hun;
        presenter.Text = score.ToString();
    }

    public void SetGlobalScoreToBar(TeamType team, GameProgress gameProgress)
    {
        GlobalScorePresenter presenter = new();

        if (team == TeamType.HomeTeam)
        {
            if (_mainGlobalScoreControls[gameProgress] is GlobalScorePresenter item)
                presenter = item;
        }
        else
        {
            if (_awayGlobalScoreControls[gameProgress] is GlobalScorePresenter item1)
                presenter = item1;
        }

        presenter.IsCampVisible = false;
        presenter.Text = "-";
    }

    /// <summary>
    /// 重置全局分数统计
    /// </summary>
    public void ResetGlobalScore()
    {
        //主队
        foreach (var progress in Enum.GetValues<GameProgress>())
        {
            if (progress != GameProgress.Free)
            {
                SetGlobalScoreToBar(TeamType.HomeTeam, progress);
            }
        }

        //客队
        foreach (var progress in Enum.GetValues<GameProgress>())
        {
            if (progress != GameProgress.Free)
            {
                SetGlobalScoreToBar(TeamType.AwayTeam, progress);
            }
        }
    }

    private double _globalScoreTotalMargin;

    private double _lastMove;


    /// <summary>
    /// 赛制切换
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnBo3ModeChanged(object? sender, EventArgs args)
    {
        _isBo3Mode = _sharedDataService.IsBo3Mode;
        if (FrontedWindows[GetFrontedWindowGuid(FrontedWindowType.ScoreGlobalWindow)] is not ScoreGlobalWindow
            scoreWindow) return;
        if (_isBo3Mode)
        {
            scoreWindow.BaseCanvas.Background =
                new ImageBrush(_settingsHostService.Settings.ScoreWindowSettings.GlobalScoreBgImageBo3);
            foreach (var item in
                     _mainGlobalScoreControls.Where(item => item.Key > GameProgress.Game3OvertimeSecondHalf))
            {
                item.Value.Visibility = Visibility.Hidden;
            }

            foreach (var item in
                     _awayGlobalScoreControls.Where(item => item.Key > GameProgress.Game3OvertimeSecondHalf))
            {
                item.Value.Visibility = Visibility.Hidden;
            }

            Canvas.SetLeft(scoreWindow.MainScoreTotal,
                Canvas.GetLeft(scoreWindow.MainScoreTotal) - _globalScoreTotalMargin);
            Canvas.SetLeft(scoreWindow.AwayScoreTotal,
                Canvas.GetLeft(scoreWindow.AwayScoreTotal) - _globalScoreTotalMargin);
            _lastMove = _globalScoreTotalMargin;
        }
        else
        {
            scoreWindow.BaseCanvas.Background =
                new ImageBrush(_settingsHostService.Settings.ScoreWindowSettings.GlobalScoreBgImage);
            foreach (var item in
                     _mainGlobalScoreControls.Where(item => item.Key > GameProgress.Game3OvertimeSecondHalf))
            {
                item.Value.Visibility = Visibility.Visible;
            }

            foreach (var item in
                     _awayGlobalScoreControls.Where(item => item.Key > GameProgress.Game3OvertimeSecondHalf))
            {
                item.Value.Visibility = Visibility.Visible;
            }

            Canvas.SetLeft(scoreWindow.MainScoreTotal, Canvas.GetLeft(scoreWindow.MainScoreTotal) + _lastMove);
            Canvas.SetLeft(scoreWindow.AwayScoreTotal, Canvas.GetLeft(scoreWindow.AwayScoreTotal) + _lastMove);
        }
    }


    /// <summary>
    /// 接收GlobalScoreTotalMargin变更
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnGlobalScoreTotalMarginChanged(object? sender, EventArgs args)
    {
        _globalScoreTotalMargin = _sharedDataService.GlobalScoreTotalMargin;
        _globalScoreTotalMargin = _sharedDataService.GlobalScoreTotalMargin;
    }

    /// <summary>
    /// 将控件添加到 Canvas 并设置位置
    /// </summary>
    private static void AddScoreGlobalControlToCanvas(FrameworkElement control, Canvas canvas, GameProgress progress,
        int top)
    {
        // 设置控件位置
        var left = CalculateLeftPosition(progress);

        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);

        //创建绑定

        //设计者模式
        var designerModeBinding =
            new Binding(nameof(ScoreWindowViewModel.IsDesignerMode))
            {
                Source = canvas.DataContext,
            };
        //文本设置
        var fontFamilyBinding =
            new Binding("Settings.TextSettings.ScoreGlobal_Data.FontFamily")
            {
                Source = canvas.DataContext,
            };
        var fontSizeBinding =
            new Binding("Settings.TextSettings.ScoreGlobal_Data.FontSize")
            {
                Source = canvas.DataContext,
            };
        var fontWeightBinding =
            new Binding("Settings.TextSettings.ScoreGlobal_Data.FontWeight")
            {
                Source = canvas.DataContext,
            };
        var foregroundBinding =
            new Binding("Settings.TextSettings.ScoreGlobal_Data.Foreground")
            {
                Source = canvas.DataContext,
            };

        BindingOperations.SetBinding(control, DesignBehavior.IsDesignerModeProperty, designerModeBinding);
        BindingOperations.SetBinding(control, Control.FontFamilyProperty, fontFamilyBinding);
        BindingOperations.SetBinding(control, Control.FontSizeProperty, fontSizeBinding);
        BindingOperations.SetBinding(control, Control.FontWeightProperty, fontWeightBinding);
        BindingOperations.SetBinding(control, Control.ForegroundProperty, foregroundBinding);

        canvas.Children.Add(control);
    }

    /// <summary>
    /// 计算控件左侧距离
    /// </summary>
    /// <param name="progress"></param>
    /// <returns></returns>
    private static double CalculateLeftPosition(GameProgress progress) => 175 + ((int)progress) * 90; // 每个控件间隔的像素

    /// <summary>
    /// 注册控件
    /// </summary>
    /// <param name="nameHeader">控件名头</param>
    /// <param name="key">控件序号 (在字典中查找用的Key)</param>
    /// <param name="elementDict">控件所在的字典</param>
    /// <param name="control">控件</param>
    /// <param name="isOverride">是否覆盖(当Key值相同的情况下)</param>
    /// <typeparam name="T">控件的Key类型</typeparam>
    /// <exception cref="ArgumentException">添加控件时，Key值已经存在</exception>
    private static void RegisterScoreGlobalControl<T>(string nameHeader, T key,
        Dictionary<T, FrameworkElement> elementDict, FrameworkElement control, bool isOverride = true)
        where T : notnull
    {
        var name = nameHeader + key.ToString();
        control.Name = name;
        if (elementDict.TryAdd(key, control)) return;
        if (!isOverride)
            throw new ArgumentException(
                $"Control with key '{key}' already exists. Set isOverride to true to replace.");
        elementDict[key] = control;
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