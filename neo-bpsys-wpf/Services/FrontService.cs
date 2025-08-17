using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.AttachedBehaviors;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Views.Windows;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 对局引导服务, 实现了 <see cref="IFrontService"/> 接口，负责与前台窗口进行交互
/// </summary>
public class FrontService : IFrontService
{
    /// <summary>
    /// 前台窗口列表
    /// </summary>
    public Dictionary<FrontWindowType, Window> FrontWindows { get; private set; } = [];

    /// <summary>
    /// 前台窗口状态列表
    /// </summary>
    public Dictionary<FrontWindowType, bool> FrontWindowStates { get; private set; } = [];

    /// <summary>
    /// 前台画布列表
    /// </summary>
    public List<(FrontWindowType, string)> FrontCanvas { get; private set; } = []; //List<string>是Canvas（们）的名称

    private static readonly Dictionary<GameProgress, FrameworkElement> MainGlobalScoreControls = [];
    private static readonly Dictionary<GameProgress, FrameworkElement> AwayGlobalScoreControls = [];

    private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
    private readonly IMessageBoxService _messageBoxService;
    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    public FrontService(
        BpWindow bpWindow,
        CutSceneWindow cutSceneWindow,
        GameDataWindow gameDataWindow,
        ScoreSurWindow scoreSurWindow,
        ScoreHunWindow scoreHunWindow,
        ScoreGlobalWindow scoreGlobalWindow,
        WidgetsWindow widgetsWindow,
        IMessageBoxService messageBoxService,
        ISharedDataService sharedDataService,
        ISettingsHostService settingsHostService
    )
    {
        _messageBoxService = messageBoxService;
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        if (!Directory.Exists(AppConstants.AppDataPath)) Directory.CreateDirectory(AppConstants.AppDataPath);
        // 注册窗口和画布
        RegisterFrontWindowAndCanvas(FrontWindowType.BpWindow, bpWindow);
        RegisterFrontWindowAndCanvas(FrontWindowType.CutSceneWindow, cutSceneWindow);
        RegisterFrontWindowAndCanvas(FrontWindowType.GameDataWindow, gameDataWindow);
        RegisterFrontWindowAndCanvas(FrontWindowType.ScoreSurWindow, scoreSurWindow);
        RegisterFrontWindowAndCanvas(FrontWindowType.ScoreHunWindow, scoreHunWindow);
        RegisterFrontWindowAndCanvas(FrontWindowType.ScoreGlobalWindow, scoreGlobalWindow);
        RegisterFrontWindowAndCanvas(FrontWindowType.WidgetsWindow, widgetsWindow, "MapBpCanvas");
        RegisterFrontWindowAndCanvas(FrontWindowType.WidgetsWindow, widgetsWindow, "BpOverViewCanvas");
        RegisterFrontWindowAndCanvas(FrontWindowType.WidgetsWindow, widgetsWindow, "MapV2Canvas");

        //注册分数统计界面的分数控件
        GlobalScoreControlsReg();

#if DEBUG
        //记录初始位置
        foreach (var i in FrontCanvas)
        {
            RecordInitialPositions(i.Item1, i.Item2);
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
    /// 从文件加载位置信息
    /// </summary>
    /// <returns></returns>
    private async Task LoadElementsPositionOnStartup()
    {
        foreach (var i in FrontCanvas)
        {
            await LoadWindowElementsPositionOnStartupAsync(i.Item1, i.Item2);
        }
    }

    /// <summary>
    /// 注册窗口和画布
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="window"></param>
    /// <param name="canvasName"></param>
    public void RegisterFrontWindowAndCanvas(FrontWindowType windowType, Window window,
        string canvasName = "BaseCanvas")
    {
        if (FrontWindows.TryAdd(windowType, window))
        {
            FrontWindowStates[windowType] = false;
        }

        if (!FrontCanvas.Contains((windowType, canvasName)))
            FrontCanvas.Add((windowType, canvasName));
    }

    public string? GetWindowName(FrontWindowType windowType)
    {
        FrontWindows.TryGetValue(windowType, out var window);
        return window?.GetType().Name;
    }

    #region 窗口显示/隐藏管理

    public void AllWindowShow()
    {
        foreach (var window in FrontWindows.Where(pair => !FrontWindowStates[pair.Key]))
        {
            window.Value.Show();
            FrontWindowStates[window.Key] = true;
        }

        Thread.Sleep(250);
        Application.Current.MainWindow?.Activate();
    }

    public void AllWindowHide()
    {
        foreach (var window in FrontWindows.Where(pair => FrontWindowStates[pair.Key]))
        {
            window.Value.Hide();
            FrontWindowStates[window.Key] = false;
        }
    }
    
    /// <summary>
    /// 显示窗口
    /// </summary>
    /// <param name="windowType"></param>
    public void ShowWindow(FrontWindowType windowType)
    {
        if (!FrontWindows.TryGetValue(windowType, out var window))
        {
            _messageBoxService.ShowErrorAsync($"未注册的窗口类型：{windowType}", "窗口启动错误");
            return;
        }

        if (FrontWindowStates[windowType])
        {
            window.Activate();
            return;
        }
        else
        {
            window.Show();
            FrontWindowStates[windowType] = true;
        }

        Thread.Sleep(250);
        Application.Current.MainWindow?.Activate();
    }
    
    /// <summary>
    /// 隐藏窗口
    /// </summary>
    /// <param name="windowType"></param>
    public void HideWindow(FrontWindowType windowType)
    {
        if (!FrontWindows.TryGetValue(windowType, out var window))
        {
            _messageBoxService.ShowErrorAsync($"未注册的窗口类型：{windowType}", "窗口关闭错误");
            return;
        }

        if (!FrontWindowStates[windowType]) return;
        window.Hide();
        FrontWindowStates[windowType] = false;
    }

    #endregion

    #region 前台动态控件添加

    /// <summary>
    /// 将控件添加到 Canvas 并设置位置
    /// </summary>
    private static void AddControlToCanvas(FrameworkElement control, Canvas canvas, GameProgress progress, int top)
    {
        // 设置控件位置
        var left = CalculateLeftPosition(progress);

        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);

        //创建控件绑定
        var binding = new Binding("IsDesignMode")
        {
            Source = canvas.DataContext,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        DesignBehavior.SetIsDesignMode(control, true); // 触发绑定
        BindingOperations.SetBinding(control, DesignBehavior.IsDesignModeProperty, binding);

        canvas.Children.Add(control);
    }

    /// <summary>
    /// 计算控件左侧距离
    /// </summary>
    /// <param name="progress"></param>
    /// <returns></returns>
    private static double CalculateLeftPosition(GameProgress progress) => 170 + ((int)progress) * 98; // 每个控件间隔 100 像素

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
    private static void RegisterControl<T>(string nameHeader, T key,
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

    #region 设计者模式

    /// <summary>
    /// 记录窗口中元素的初始位置 (仅在DEBUG下有效)
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="canvasName">画布名称</param>
    private void RecordInitialPositions(FrontWindowType windowType, string canvasName = "BaseCanvas")
    {
        if (!FrontWindows.TryGetValue(windowType, out var window)) return;
        var path = Path.Combine(AppConstants.AppDataPath, $"{window.GetType().Name}Config-{canvasName}.default.json");

        if (File.Exists(path)) return;

        var positions = GetElementsPositions(window, canvasName);
        if (positions == null) return;
        var output = JsonSerializer.Serialize(positions, _jsonSerializerOptions);
        try
        {
            File.WriteAllText(path, output);
        }
        catch (Exception ex)
        {
            _messageBoxService.ShowErrorAsync(ex.Message, "生成默认前台配置文件发生错误");
        }
    }

    /// <summary>
    /// 获取窗口中元素的位置信息
    /// </summary>
    /// <param name="window"></param>
    /// <param name="canvasName"></param>
    /// <returns></returns>
    private static Dictionary<string, ElementInfo>? GetElementsPositions(Window window, string canvasName)
    {
        if (window.FindName(canvasName) is not Canvas canvas)
            return null;

        var positions = new Dictionary<string, ElementInfo>();
        foreach (UIElement child in canvas.Children)
        {
            if (child is not FrameworkElement fe || string.IsNullOrEmpty(fe.Name)) continue;
            if (fe.Tag?.ToString() == "nv") continue;

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
    public void SaveWindowCanvasElementsPosition(FrontWindowType windowType, string canvasName = "BaseCanvas")
    {
        if (!FrontWindows.TryGetValue(windowType, out var window))
        {
            _messageBoxService.ShowErrorAsync($"未注册的窗口类型：{windowType}", "配置文件保存错误");
            return;
        }

        if (windowType == FrontWindowType.ScoreGlobalWindow && canvasName == "ScoreGlobalCanvas" && _isBo3Mode) return;

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
            _messageBoxService.ShowInfoAsync($"保存前台配置文件失败\n{ex.Message}", "保存提示");
        }
    }

    /// <summary>
    /// 保存指定窗口的元素位置信息
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    public void SaveWindowElementsPosition(FrontWindowType windowType)
    {
        if (windowType == FrontWindowType.ScoreWindow)
        {
            SaveWindowElementsPosition(FrontWindowType.ScoreSurWindow);
            SaveWindowElementsPosition(FrontWindowType.ScoreHunWindow);
            SaveWindowElementsPosition(FrontWindowType.ScoreGlobalWindow);
        }

        foreach (var tuple in FrontCanvas.Where(x =>
                     x.Item1 == windowType))
        {
            SaveWindowCanvasElementsPosition(tuple.Item1, tuple.Item2);
        }
    }

    /// <summary>
    /// 批量保存所有窗口中元素位置信息
    /// </summary>
    public void SaveAllWindowElementsPosition()
    {
        foreach (var i in FrontCanvas)
        {
            SaveWindowCanvasElementsPosition(i.Item1, i.Item2);
        }
    }

    /// <summary>
    /// 程序启动时从JSON中加载窗口中元素的位置信息
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="canvasName">画布名称</param>
    private async Task LoadWindowElementsPositionOnStartupAsync(FrontWindowType windowType,
        string canvasName = "BaseCanvas")
    {
        if (!FrontWindows.TryGetValue(windowType, out var window))
        {
            await _messageBoxService.ShowErrorAsync($"未注册的窗口类型：{windowType}", "配置文件加载错误");
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
            File.Move(path, path + ".disabled", true);
            await _messageBoxService.ShowErrorAsync(ex.Message);
        }
    }

    /// <summary>
    /// 从JSON中加载窗口中元素位置信息
    /// </summary>
    /// <param name="canvasName">画布名称</param>
    /// <param name="jsonContent">JSON内容</param>
    /// <param name="window">窗口实例</param>
    private static void LoadElementsPositions(string canvasName, string jsonContent, Window window)
    {
        var positions = JsonSerializer.Deserialize<Dictionary<string, ElementInfo>>(jsonContent);

        if (window.FindName(canvasName) is not Canvas canvas || positions == null) return;
        foreach (UIElement child in canvas.Children)
        {
            if (child is not FrameworkElement fe ||
                !positions.TryGetValue(fe.Name, out var value)) continue;
            if (fe.Tag?.ToString() == "nv") continue;

            if (value.Width != null)
                fe.Width = (double)value.Width;
            if (value.Height != null)
                fe.Height = (double)value.Height;
            if (value.Left != null)
                Canvas.SetLeft(fe, (double)value.Left);
            if (value.Top != null)
                Canvas.SetTop(fe, (double)value.Top);
        }
    }

    /// <summary>
    /// 还原窗口中的元素到初始位置
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="canvasName">画布名称</param>
    public async Task RestoreInitialPositions(FrontWindowType windowType, string canvasName = "BaseCanvas")
    {
        if (!FrontWindows.TryGetValue(windowType, out var window))
        {
            await _messageBoxService.ShowErrorAsync($"未注册的窗口类型：{windowType}", "前台默认配置恢复错误");
            return;
        }

        if (!await _messageBoxService.ShowConfirmAsync("重置提示", $"确认重置{window.GetType().Name}-{canvasName}的配置吗？"))
            return;

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "Resources", "FrontDefaultPositions", $"{window.GetType().Name}Config-{canvasName}.default.json");

        try
        {
            var json = await File.ReadAllTextAsync(path);
            LoadElementsPositions(canvasName, json, window);

            var customFilePath = Path.Combine(AppConstants.AppDataPath, $"{window.GetType().Name}Config-{canvasName}.json");

            if (File.Exists(customFilePath))
                File.Move(customFilePath, customFilePath + ".disabled", true);

            if (File.Exists(path) && Directory.Exists(AppConstants.AppDataPath))
                File.Copy(path, customFilePath, true);
        }
        catch (Exception ex)
        {
            await _messageBoxService.ShowErrorAsync(ex.Message, "读取前台默认配置错误");
        }
    }


    /// <summary>
    /// 窗口中元素位置信息
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="left"></param>
    /// <param name="top"></param>
    private class ElementInfo(double? width, double? height, double? left, double? top)
    {
        public double? Width { get; } = width;
        public double? Height { get; } = height;
        public double? Left { get; set; } = left;
        public double? Top { get; set; } = top;
    }

    #endregion

    #region 分数统计

    private bool _isBo3Mode;

    /// <summary>
    /// 注册全局计分板控件
    /// </summary>
    private void GlobalScoreControlsReg()
    {
        if (FrontWindows[FrontWindowType.ScoreGlobalWindow].FindName("BaseCanvas") is not Canvas canvas) return;
        //主队
        foreach (var progress in Enum.GetValues<GameProgress>())
        {
            if (progress == GameProgress.Free) continue;
            var control = new GlobalScorePresenter();
            RegisterControl("MainTeam", progress, MainGlobalScoreControls, control);
        }

        //客队
        foreach (var progress in Enum.GetValues<GameProgress>())
        {
            if (progress == GameProgress.Free) continue;
            var control = new GlobalScorePresenter();
            RegisterControl("Away", progress, AwayGlobalScoreControls, control);
        }

        //添加控件到 Canvas 并设置位置
        foreach (var item in MainGlobalScoreControls)
        {
            AddControlToCanvas(item.Value, canvas, item.Key, 86);
            SetBinding(item.Value, TextBlock.FontSizeProperty, "Settings.TextSettings.ScoreGlobal_Data.FontSize");
            SetBinding(item.Value, TextBlock.FontFamilyProperty, "Settings.TextSettings.ScoreGlobal_Data.FontFamily");
            SetBinding(item.Value, TextBlock.FontWeightProperty, "Settings.TextSettings.ScoreGlobal_Data.FontWeight");
            SetBinding(item.Value, TextBlock.ForegroundProperty, "Settings.TextSettings.ScoreGlobal_Data.Foreground");
        }

        foreach (var item in AwayGlobalScoreControls)
        {
            AddControlToCanvas(item.Value, canvas, item.Key, 147);
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

        if (team == TeamType.MainTeam)
        {
            if (MainGlobalScoreControls[gameProgress] is GlobalScorePresenter item)
                presenter = item;
        }
        else
        {
            if (AwayGlobalScoreControls[gameProgress] is GlobalScorePresenter item1)
                presenter = item1;
        }

        presenter.IsCampVisible = true;
        presenter.IsHunIcon = camp == Camp.Hun;
        presenter.Text = score.ToString();
    }

    public void SetGlobalScoreToBar(TeamType team, GameProgress gameProgress)
    {
        GlobalScorePresenter presenter = new();

        if (team == TeamType.MainTeam)
        {
            if (MainGlobalScoreControls[gameProgress] is GlobalScorePresenter item)
                presenter = item;
        }
        else
        {
            if (AwayGlobalScoreControls[gameProgress] is GlobalScorePresenter item1)
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
                SetGlobalScoreToBar(TeamType.MainTeam, progress);
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
        if (FrontWindows[FrontWindowType.ScoreGlobalWindow] is not ScoreGlobalWindow scoreWindow) return;
        if (_isBo3Mode)
        {
            scoreWindow.BaseCanvas.Background =
                new ImageBrush(_settingsHostService.Settings.ScoreWindowSettings.GlobalScoreBgImageBo3);
            foreach (var item in
                     MainGlobalScoreControls.Where(item => item.Key > GameProgress.Game3ExtraSecondHalf))
            {
                item.Value.Visibility = Visibility.Hidden;
            }

            foreach (var item in
                     AwayGlobalScoreControls.Where(item => item.Key > GameProgress.Game3ExtraSecondHalf))
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
                     MainGlobalScoreControls.Where(item => item.Key > GameProgress.Game3ExtraSecondHalf))
            {
                item.Value.Visibility = Visibility.Visible;
            }

            foreach (var item in
                     AwayGlobalScoreControls.Where(item => item.Key > GameProgress.Game3ExtraSecondHalf))
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

    #endregion

    #region 动画

    /// <summary>
    /// 渐显动画
    /// </summary>
    /// <param name="windowType">窗体类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    public void FadeInAnimation(FrontWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontWindows.TryGetValue(windowType, out var window)) return;

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
    public void FadeOutAnimation(FrontWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontWindows.TryGetValue(windowType, out var window)) return;
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
    public async Task BreathingStart(FrontWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontWindows.TryGetValue(windowType, out var window)) return;
        if (window.FindName(ctrName) is not FrameworkElement element) return;

        element.Opacity = 0;
        element.Visibility = Visibility.Visible;
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.25))));
        await Task.Delay(250);

        // 如果已有动画，先停止
        await BreathingStop(windowType, controlNameHeader, controlIndex, controlNameFooter);

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
    public async Task BreathingStop(FrontWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter)
    {
        var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
        if (!FrontWindows.TryGetValue(windowType, out var window)) return;
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