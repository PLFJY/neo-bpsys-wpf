using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.AttachedBehaviors;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Views.Windows;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Models;
using Path = System.IO.Path;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 对局引导服务, 实现了 <see cref="IFrontService"/> 接口，负责与前台窗口进行交互
    /// </summary>
    public class FrontService : IFrontService
    {
        private readonly Dictionary<Type, Window> _frontWindows = [];
        public Dictionary<Type, bool> FrontWindowStates { get; } = [];

        private readonly List<(Window, string)> _frontCanvas = []; //List<string>是Canvas（们）的名称

        private static readonly Dictionary<GameProgress, FrameworkElement> MainGlobalScoreControls = [];
        private static readonly Dictionary<GameProgress, FrameworkElement> AwayGlobalScoreControls = [];

        private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
        private readonly IMessageBoxService _messageBoxService;
        private readonly ISettingsHostService _settingsHostService;

        public FrontService(
            BpWindow bpWindow,
            CutSceneWindow cutSceneWindow,
            GameDataWindow gameDataWindow,
            ScoreWindow scoreWindow,
            WidgetsWindow widgetsWindow,
            IMessageBoxService messageBoxService,
            ISharedDataService sharedDataService,
            ISettingsHostService settingsHostService
        )
        {
            _messageBoxService = messageBoxService;
            _settingsHostService = settingsHostService;
            var savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf");
            if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);
            // 注册窗口和画布
            RegisterFrontWindowAndCanvas(bpWindow);
            RegisterFrontWindowAndCanvas(cutSceneWindow);
            RegisterFrontWindowAndCanvas(gameDataWindow);
            RegisterFrontWindowAndCanvas(scoreWindow, "ScoreSurCanvas");
            RegisterFrontWindowAndCanvas(scoreWindow, "ScoreHunCanvas");
            RegisterFrontWindowAndCanvas(scoreWindow, "ScoreGlobalCanvas");
            RegisterFrontWindowAndCanvas(widgetsWindow, "MapBpCanvas");
            RegisterFrontWindowAndCanvas(gameDataWindow);

            //注册分数统计界面的分数控件
            GlobalScoreControlsReg();

#if DEBUG
            // 记录初始位置
            foreach (var i in _frontCanvas)
            {
                RecordInitialPositions(i.Item1, i.Item2);
            }
#endif
            _isBo3Mode = sharedDataService.IsBo3Mode;
            _globalScoreTotalMargin = sharedDataService.GlobalScoreTotalMargin;
            WeakReferenceMessenger.Default.Register<PropertyChangedMessage<bool>>(this, BoolPropertyChangedRecipient);
            WeakReferenceMessenger.Default.Register<PropertyChangedMessage<double>>(this,
                DoublePropertyChangedRecipient);
            OnBo3ModeChanged();
            
            settingsHostService.LoadConfig();
            ApplyAllWindowsSettings();
        }

        private void DoublePropertyChangedRecipient(object recipient, PropertyChangedMessage<double> message)
        {
            if (message.PropertyName == nameof(ISharedDataService.GlobalScoreTotalMargin))
            {
                _globalScoreTotalMargin = message.NewValue;
            }
        }

        /// <summary>
        /// 注册窗口和画布
        /// </summary>
        /// <param name="window"></param>
        /// <param name="canvasName"></param>
        private void RegisterFrontWindowAndCanvas(Window window, string canvasName = "BaseCanvas")
        {
            var type = window.GetType();

            if (_frontWindows.TryAdd(type, window))
            {
                FrontWindowStates[type] = false;
            }

            if (!_frontCanvas.Contains((window, canvasName)))
                _frontCanvas.Add((window, canvasName));
        }

        #region 窗口显示/隐藏管理

        public void AllWindowShow()
        {
            foreach (var window in _frontWindows.Values.Where(window => !FrontWindowStates[window.GetType()]))
            {
                window.Show();
                FrontWindowStates[window.GetType()] = true;
            }
            Application.Current.MainWindow?.Activate();
        }

        public void AllWindowHide()
        {
            foreach (var window in _frontWindows.Values.Where(window => FrontWindowStates[window.GetType()]))
            {
                window.Hide();
                FrontWindowStates[window.GetType()] = false;
            }
        }

        public void ShowWindow<T>() where T : Window
        {
            if (!_frontWindows.TryGetValue(typeof(T), out var window))
            {
                _messageBoxService.ShowErrorAsync($"未注册的窗口类型：{typeof(T)}", "窗口启动错误");
                return;
            }

            if (FrontWindowStates[typeof(T)]) window.Activate();
            window.Show();
            FrontWindowStates[typeof(T)] = true;

            Application.Current.MainWindow?.Activate();
        }

        public void HideWindow<T>() where T : Window
        {
            if (!_frontWindows.TryGetValue(typeof(T), out var window))
            {
                _messageBoxService.ShowErrorAsync($"未注册的窗口类型：{typeof(T)}", "窗口关闭错误");
                return;
            }

            if (!FrontWindowStates[typeof(T)]) return;
            window.Hide();
            FrontWindowStates[typeof(T)] = false;
        }

        #endregion

        #region 前台动态控件添加

        /// <summary>
        /// 将控件添加到 Canvas 并设置位置
        /// </summary>
        private static void AddControlToCanvas(FrameworkElement control, Canvas canvas, GameProgress progress, int top)
        {
            // 设置控件位置（示例逻辑）
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

        private static double CalculateLeftPosition(GameProgress progress)
        {
            // 示例：根据枚举值计算水平位置
            return 170 + ((int)progress) * 98; // 每个控件间隔 100 像素
        }

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
        /// <param name="window">该窗口的实例</param>
        /// <param name="canvasName"></param>
        private void RecordInitialPositions(Window window, string canvasName = "BaseCanvas")
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.default.json");

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
        /// 保存窗口中元素的位置信息
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <param name="canvasName">画布名称</param>
        public void SaveWindowElementsPosition<T>(string canvasName = "BaseCanvas") where T : Window
        {
            if (!_frontWindows.TryGetValue(typeof(T), out var window))
            {
                _messageBoxService.ShowErrorAsync($"未注册的窗口类型：{typeof(T)}", "配置文件保存错误");
                return;
            }

            if (typeof(T) == typeof(ScoreWindow) && canvasName == "ScoreGlobalCanvas" && _isBo3Mode) return;

            var positions = GetElementsPositions(window, canvasName);
            if (positions == null) return;

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.json");
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
        /// 程序启动时从JSON中加载窗口中元素的位置信息
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <param name="canvasName">画布名称</param>
        public async Task LoadWindowElementsPositionOnStartupAsync<T>(string canvasName = "BaseCanvas") where T : Window
        {
            if (!_frontWindows.TryGetValue(typeof(T), out var window))
            {
                await _messageBoxService.ShowErrorAsync($"未注册的窗口类型：{typeof(T)}", "配置文件加载错误");
                return;
            }

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.json");
            if (!File.Exists(path)) return;

            try
            {
                var jsonContent = await File.ReadAllTextAsync(path);
                LoadElementsPositions<T>(canvasName, jsonContent, window);
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
        /// <param name="canvasName"></param>
        /// <param name="jsonContent"></param>
        /// <param name="window"></param>
        /// <typeparam name="T"></typeparam>
        private static void LoadElementsPositions<T>(string canvasName, string jsonContent, Window window)
            where T : Window
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
        /// <typeparam name="T"></typeparam>
        /// <param name="canvasName"></param>
        public async Task RestoreInitialPositions<T>(string canvasName = "BaseCanvas") where T : Window
        {
            if (!_frontWindows.TryGetValue(typeof(T), out var window))
            {
                await _messageBoxService.ShowErrorAsync($"未注册的窗口类型：{typeof(T)}", "前台默认配置恢复错误");
                return;
            }

            if (!await _messageBoxService.ShowConfirmAsync("重置提示", $"确认重置{window.GetType()}-{canvasName}的配置吗？")) return;

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "Resources", "FrontDefaultPositions", $"{window.GetType().Name}Config-{canvasName}.default.json");

            try
            {
                var json = await File.ReadAllTextAsync(path);
                LoadElementsPositions<T>(canvasName, json, window);

                var customFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.json");

                if (File.Exists(customFilePath))
                    File.Move(customFilePath, customFilePath + ".disabled", true);

                if (File.Exists(path) && Directory.Exists(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "neo-bpsys-wpf")))
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
            if (_frontWindows[typeof(ScoreWindow)].FindName("ScoreGlobalCanvas") is not Canvas canvas) return;
            //主队
            foreach (var progress in Enum.GetValues<GameProgress>())
            {
                if (progress == GameProgress.Free) continue;
                var control = new GlobalScorePresenter();
                RegisterControl("Main", progress, MainGlobalScoreControls, control);
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
            }

            foreach (var item in AwayGlobalScoreControls)
            {
                AddControlToCanvas(item.Value, canvas, item.Key, 147);
            }
        }

        /// <summary>
        /// 设置分数统计
        /// </summary>
        /// <param name="team"></param>
        /// <param name="gameProgress"></param>
        /// <param name="camp"></param>
        /// <param name="score"></param>
        public void SetGlobalScore(string team, GameProgress gameProgress, Camp camp, int score)
        {
            GlobalScorePresenter presenter = new();

            if (team == nameof(ISharedDataService.MainTeam))
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

        public void SetGlobalScoreToBar(string team, GameProgress gameProgress)
        {
            GlobalScorePresenter presenter = new();

            if (team == nameof(ISharedDataService.MainTeam))
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
            foreach (GameProgress progress in Enum.GetValues<GameProgress>())
            {
                if (progress != GameProgress.Free)
                {
                    SetGlobalScoreToBar(nameof(ISharedDataService.MainTeam), progress);
                }
            }

            //客队
            foreach (GameProgress progress in Enum.GetValues<GameProgress>())
            {
                if (progress != GameProgress.Free)
                {
                    SetGlobalScoreToBar(nameof(ISharedDataService.AwayTeam), progress);
                }
            }
        }

        private double _globalScoreTotalMargin;

        private double _lastMove;

        /// <summary>
        /// 接收到切换赛制的消息
        /// </summary>
        /// <param name="recipient"></param>
        /// <param name="message"></param>
        private void BoolPropertyChangedRecipient(object recipient, PropertyChangedMessage<bool> message)
        {
            if (message.PropertyName != nameof(ISharedDataService.IsBo3Mode)) return;
            _isBo3Mode = message.NewValue;
            OnBo3ModeChanged();
        }

        private void OnBo3ModeChanged()
        {
            if (_frontWindows[typeof(ScoreWindow)] is not ScoreWindow scoreWindow) return;
            if (_isBo3Mode)
            {
                scoreWindow.ScoreGlobalCanvas.Background = ImageHelper.GetUiImageBrush("scoreGlobal_Bo3");
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
                scoreWindow.ScoreGlobalCanvas.Background = ImageHelper.GetUiImageBrush("scoreGlobal");
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

        #endregion

        #region 动画

        /// <summary>
        /// 渐显动画
        /// </summary>
        /// <param name="controlNameHeader"></param>
        /// <param name="controlIndex"></param>
        /// <param name="controlNameFooter"></param>
        /// <typeparam name="T"></typeparam>
        public void FadeInAnimation<T>(string controlNameHeader, int controlIndex, string controlNameFooter)
            where T : Window
        {
            var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
            if (_frontWindows[typeof(T)] is not T window) return;

            if (window.FindName(ctrName) is FrameworkElement element)
            {
                element.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5))));
            }
        }

        /// <summary>
        /// 渐隐动画
        /// </summary>
        /// <param name="controlNameHeader"></param>
        /// <param name="controlIndex"></param>
        /// <param name="controlNameFooter"></param>
        /// <typeparam name="T"></typeparam>
        public void FadeOutAnimation<T>(string controlNameHeader, int controlIndex, string controlNameFooter)
            where T : Window
        {
            var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
            if (_frontWindows[typeof(T)] is not T window) return;
            if (window.FindName(ctrName) is FrameworkElement element)
            {
                element.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5))));
            }
        }

        /// <summary>
        /// 呼吸动画开始
        /// </summary>
        /// <param name="controlNameHeader"></param>
        /// <param name="controlIndex"></param>
        /// <param name="controlNameFooter"></param>
        /// <typeparam name="T"></typeparam>
        public async Task BreathingStart<T>(string controlNameHeader, int controlIndex, string controlNameFooter)
            where T : Window
        {
            var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
            if (_frontWindows[typeof(T)] is not T window) return;
            if (window.FindName(ctrName) is not FrameworkElement element) return;

            element.Opacity = 0;
            element.Visibility = Visibility.Visible;
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5))));
            await Task.Delay(500);

            // 如果已有动画，先停止
            await BreathingStop<T>(controlNameHeader, controlIndex, controlNameFooter);

            var animation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.5,
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
        /// <param name="controlNameHeader"></param>
        /// <param name="controlIndex"></param>
        /// <param name="controlNameFooter"></param>
        /// <typeparam name="T"></typeparam>
        public async Task BreathingStop<T>(string controlNameHeader, int controlIndex, string controlNameFooter)
            where T : Window
        {
            var ctrName = controlNameHeader + (controlIndex >= 0 ? controlIndex : string.Empty) + controlNameFooter;
            if (_frontWindows[typeof(T)] is not T window) return;
            if (window.FindName(ctrName) is not FrameworkElement element) return;
            if (element.Tag is not Storyboard storyboard) return;

            storyboard.Stop();
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5))));
            await Task.Delay(500);

            element.Opacity = 0; // 恢复初始状态
            element.Tag = null;
            element.Visibility = Visibility.Hidden;
        }

        #endregion

        #region 设置读取

        /// <summary>
        /// 应用设置
        /// </summary>
        /// <param name="windowType">窗口类型</param>
        /// <param name="isInitial">是否是初始化模式</param>
        public void ApplySettings(Type windowType, bool isInitial = false)
        {
            //bp前台窗口
            if (windowType == typeof(BpWindow))
            {
                //获取窗口实例
                if (_frontWindows[typeof(BpWindow)] is not BpWindow window) return;
                //获取设置项
                var setting = _settingsHostService.Settings.BpWindowSettings;

                //设置Bp背景
                if (!string.IsNullOrEmpty(setting.BgImageUri))
                    window.BaseCanvas.Background = new ImageBrush(new BitmapImage(new Uri(setting.BgImageUri)));

                //设置当局禁选锁
                if (!string.IsNullOrEmpty(setting.CurrentBanLockImageUri))
                    for (var i = 0; i < 4; i++)
                    {
                        if (window.FindName($"HunBanCurrentLock{i}") is Image hunBanCurrentLock)
                            hunBanCurrentLock.Source = new BitmapImage(new Uri(setting.CurrentBanLockImageUri));

                        if (window.FindName($"SurBanCurrentLock{i}") is Image surBanCurrentLock)
                            surBanCurrentLock.Source = new BitmapImage(new Uri(setting.CurrentBanLockImageUri));
                    }

                //设置全局禁选锁
                if (!string.IsNullOrEmpty(setting.GlobalBanLockImageUri))
                    for (var i = 0; i < 9; i++)
                    {
                        if (window.FindName($"HunGlobalBanLock{i}") is Image feHun)
                            feHun.Source = new BitmapImage(new Uri(setting.GlobalBanLockImageUri));

                        if (window.FindName($"HunGlobalBanLock{i}") is Image feSur)
                            feSur.Source = new BitmapImage(new Uri(setting.GlobalBanLockImageUri));
                    }

                //待选框
                if (!string.IsNullOrEmpty(setting.PickingBorderImageUri))
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (window.FindName($"SurPickingBorder{i}") is Image fe)
                            fe.Source = new BitmapImage(new Uri(setting.PickingBorderImageUri));
                    }
                }

                //倒计时
                TextSettingApply(window.Timer, setting.TextSettings.Timer);

                //队伍名称
                TextSettingApply(window.SurTeamName, setting.TextSettings.TeamName);
                TextSettingApply(window.HunTeamName, setting.TextSettings.TeamName);

                //小比分
                TextSettingApply(window.MinorPointsSur, setting.TextSettings.MinorPoints);
                TextSettingApply(window.MinorPointsHun, setting.TextSettings.MinorPoints);

                //大比分
                TextSettingApply(window.SurTeamMajorPoint, setting.TextSettings.MajorPoints);
                TextSettingApply(window.HunTeamMajorPoint, setting.TextSettings.MajorPoints);

                //选手id
                for (var i = 0; i < 4; i++)
                {
                    if (window.FindName($"SurId{i}") is Border { Child: TextBlock surId })
                        TextSettingApply(surId, setting.TextSettings.PlayerId);
                }

                if (window.HunId.Child is TextBlock hunId)
                    TextSettingApply(hunId, setting.TextSettings.PlayerId);

                //地图名称
                if (window.MapName.Child is TextBlock mapName)
                    TextSettingApply(mapName, setting.TextSettings.MapName);

                //对局进度
                if (window.GameProgress.Child is TextBlock gameProgress)
                    TextSettingApply(gameProgress, setting.TextSettings.GameProgress);
            }

            //过场画面窗口
            if (windowType == typeof(CutSceneWindow))
            {
                //获取窗口实例
                if (_frontWindows[typeof(CutSceneWindow)] is not CutSceneWindow window) return;
                //获取设置项
                var setting = _settingsHostService.Settings.CutSceneWindowSettings;

                //过场背景图
                if (!string.IsNullOrEmpty(setting.BgUri))
                    window.BaseCanvas.Background = new ImageBrush(new BitmapImage(new Uri(setting.BgUri)));

                //设置队伍名称
                TextSettingApply(window.SurTeamName, setting.TextSettings.TeamName);
                TextSettingApply(window.HunTeamName, setting.TextSettings.TeamName);

                //大比分
                TextSettingApply(window.SurTeamMajorPoint, setting.TextSettings.MajorPoints);
                TextSettingApply(window.HunTeamMajorPoint, setting.TextSettings.MajorPoints);

                //地图名称
                if (window.MapName.Child is TextBlock mapName)
                    TextSettingApply(mapName, setting.TextSettings.MapName);

                //对局进度
                if (window.GameProgress.Child is TextBlock gameProgress)
                    TextSettingApply(gameProgress, setting.TextSettings.GameProgress);

                for (var i = 0; i < 4; i++)
                {
                    if (window.FindName($"SurId{i}") is Border { Child: TextBlock surId })
                        TextSettingApply(surId, setting.TextSettings.PlayerId);
                }

                if (window.HunId.Child is TextBlock hunId)
                    TextSettingApply(hunId, setting.TextSettings.PlayerId);
            }

            //比分窗口
            if (windowType == typeof(ScoreWindow))
            {
                //获取窗口实例
                if (_frontWindows[typeof(ScoreWindow)] is not ScoreWindow window) return;
                //获取设置项
                var setting = _settingsHostService.Settings.ScoreWindowSettings;

                //背景UI
                if (!string.IsNullOrEmpty(setting.SurScoreBgImageUri))
                    window.ScoreSurCanvas.Background =
                        new ImageBrush(new BitmapImage(new Uri(setting.SurScoreBgImageUri)));

                if (!string.IsNullOrEmpty(setting.HunScoreBgImageUri))
                    window.ScoreHunCanvas.Background =
                        new ImageBrush(new BitmapImage(new Uri(setting.HunScoreBgImageUri)));

                if (!string.IsNullOrEmpty(setting.GlobalScoreBgImageUri))
                    window.ScoreGlobalCanvas.Background =
                        new ImageBrush(new BitmapImage(new Uri(setting.GlobalScoreBgImageUri)));

                //队伍名称
                TextSettingApply(window.SurTeamName, setting.TextSettings.TeamName);
                TextSettingApply(window.HunTeamName, setting.TextSettings.TeamName);
                TextSettingApply(window.MainTeamName, setting.TextSettings.ScoreGlobal_TeamName);
                TextSettingApply(window.AwayTeamName, setting.TextSettings.ScoreGlobal_TeamName);

                //小比分
                if (window.MinorPointsSur.Child is TextBlock minorPointsSur)
                    TextSettingApply(minorPointsSur, setting.TextSettings.MinorPoints);

                if (window.MinorPointsHun.Child is TextBlock minorPointsHun)
                    TextSettingApply(minorPointsHun, setting.TextSettings.MinorPoints);

                //大比分
                TextSettingApply(window.SurTeamMajorPoint, setting.TextSettings.MajorPoints);
                TextSettingApply(window.HunTeamMajorPoint, setting.TextSettings.MajorPoints);

                //分数统计总小比分
                if (window.MainScoreTotal.Child is TextBlock mainScoreTotal)
                    TextSettingApply(mainScoreTotal, setting.TextSettings.ScoreGlobal_Total);

                if (window.AwayScoreTotal.Child is TextBlock awayScoreTotal)
                    TextSettingApply(awayScoreTotal, setting.TextSettings.ScoreGlobal_Total);

                //分数统计
                foreach (var fe in MainGlobalScoreControls)
                {
                    if (fe.Value is not TextBlock tb) continue;
                    TextSettingApply(tb, setting.TextSettings.ScoreGlobal_Data);
                }

                foreach (var fe in AwayGlobalScoreControls)
                {
                    if (fe.Value is not TextBlock tb) continue;
                    TextSettingApply(tb, setting.TextSettings.ScoreGlobal_Data);
                }
            }

            //赛后数据窗口
            if (windowType == typeof(GameDataWindow))
            {
                //获取窗口实例
                if (_frontWindows[typeof(GameDataWindow)] is not GameDataWindow window) return;
                //获取设置项
                var setting = _settingsHostService.Settings.GameDataWindowSettings;

                //背景UI
                if (!string.IsNullOrEmpty(setting.BgImageUri))
                    window.BaseCanvas.Background = new ImageBrush(new BitmapImage(new Uri(setting.BgImageUri)));

                //队伍名称
                TextSettingApply(window.SurTeamName, setting.TextSettings.TeamName);
                TextSettingApply(window.HunTeamName, setting.TextSettings.TeamName);

                //小比分
                TextSettingApply(window.MinorPointsSur, setting.TextSettings.MinorPoints);
                TextSettingApply(window.MinorPointsHun, setting.TextSettings.MinorPoints);

                //大比分
                TextSettingApply(window.SurTeamMajorPoint, setting.TextSettings.MajorPoints);
                TextSettingApply(window.MinorPointsHun, setting.TextSettings.MajorPoints);

                //地图名称
                if (window.MapName.Child is TextBlock mapName)
                    TextSettingApply(mapName, setting.TextSettings.MapName);

                //对局进度
                if (window.GameProgress.Child is TextBlock gameProgress)
                    TextSettingApply(gameProgress, setting.TextSettings.GameProgress);

                //求生者数据
                for (var i = 0; i < 4; i++)
                {
                    //选手ID
                    if (window.FindName($"SurId{i}") is Border { Child: TextBlock surId })
                        TextSettingApply(surId, setting.TextSettings.PlayerId);

                    //破译进度
                    if (window.FindName($"Sur{i}MachineDecoded") is Border { Child: StackPanel surMachineDecodedPanel })
                        foreach (var element in surMachineDecodedPanel.Children)
                        {
                            if (element is TextBlock tb)
                                TextSettingApply(tb, setting.TextSettings.SurData);
                        }

                    //砸板命中次数
                    if (window.FindName($"Sur{i}PalletStunTimes") is Border { Child: TextBlock surPalletStunTimes })
                        TextSettingApply(surPalletStunTimes, setting.TextSettings.SurData);

                    //救人次数
                    if (window.FindName($"Sur{i}RescueTimes") is Border { Child: TextBlock surRescueTimes })
                        TextSettingApply(surRescueTimes, setting.TextSettings.SurData);

                    //治疗次数
                    if (window.FindName($"Sur{i}HealedTimes") is Border { Child: TextBlock surHealedTimes })
                        TextSettingApply(surHealedTimes, setting.TextSettings.SurData);

                    //牵制时长
                    if (window.FindName($"Sur{i}KiteTime") is Border { Child: TextBlock surKiteTime })
                        TextSettingApply(surKiteTime, setting.TextSettings.SurData);
                }

                //监管者数据
                //选手Id
                if (window.HunId.Child is TextBlock hunId)
                    TextSettingApply(hunId, setting.TextSettings.PlayerId);

                //剩余密码机数量
                if (window.HunMachineLeft.Child is TextBlock hunMachineLeft)
                    TextSettingApply(hunMachineLeft, setting.TextSettings.HunData);

                //破坏板子数
                if (window.HunPalletBroken.Child is TextBlock hunPalletBroken)
                    TextSettingApply(hunPalletBroken, setting.TextSettings.HunData);

                //命中求生者次数
                if (window.HunHitTimes.Child is TextBlock hunHitTimes)
                    TextSettingApply(hunHitTimes, setting.TextSettings.HunData);

                //恐惧震慑次数
                if (window.HunTerrorShockTimes.Child is TextBlock hunTerrorShockTimes)
                    TextSettingApply(hunTerrorShockTimes, setting.TextSettings.HunData);

                //击倒次数
                if (window.HunDownTimes.Child is TextBlock hunDownTimes)
                    TextSettingApply(hunDownTimes, setting.TextSettings.HunData);
            }

            //小组件窗口
            if (windowType == typeof(WidgetsWindow))
            {
                //获取窗口实例
                if (_frontWindows[typeof(WidgetsWindow)] is not WidgetsWindow window) return;
                //获取设置项
                var setting = _settingsHostService.Settings.WidgetsWindowSettings;

                //地图BP
                //设置UI背景
                if (!string.IsNullOrEmpty(setting.MapBpBgUri))
                    window.MapBpCanvas.Background = new ImageBrush(new BitmapImage(new Uri(setting.MapBpBgUri)));

                //地图名称
                if (window.PickedMapName.Child is TextBlock pickedMapName)
                    TextSettingApply(pickedMapName, setting.TextSettings.MapBp_MapName);

                if (window.BannedMapName.Child is TextBlock bannedMapName)
                    TextSettingApply(bannedMapName, setting.TextSettings.MapBp_MapName);

                //选择和禁用的字
                if (window.PickWord.Child is TextBlock pickWord)
                    TextSettingApply(pickWord, setting.TextSettings.MapBp_PickWord);

                if (window.BanWord.Child is TextBlock banWord)
                    TextSettingApply(banWord, setting.TextSettings.MapBp_BanWord);

                //队伍名称
                if (window.SurTeamName.Child is TextBlock surTeamName)
                    TextSettingApply(surTeamName, setting.TextSettings.MapBp_TeamName);

                if (window.HunTeamName.Child is TextBlock hunTeamName)
                    TextSettingApply(hunTeamName, setting.TextSettings.MapBp_TeamName);

                //bp总览
                //设置UI背景
                if (!string.IsNullOrEmpty(setting.BpOverviewBgUri))
                    window.BpOverViewCanvas.Background =
                        new ImageBrush(new BitmapImage(new Uri(setting.BpOverviewBgUri)));

                //队伍名称
                TextSettingApply(window.SurTeamNameInOverview, setting.TextSettings.BpOverview_TeamName);
                TextSettingApply(window.HunTeamNameInOverview, setting.TextSettings.BpOverview_TeamName);

                //对局进度
                if (window.GameProgress.Child is TextBlock gameProgress)
                    TextSettingApply(gameProgress, setting.TextSettings.BpOverview_GameProgress);

                //小比分
                TextSettingApply(window.MinorPointsSur, setting.TextSettings.BpOverview_MinorPoints);
                TextSettingApply(window.MinorPointsHun, setting.TextSettings.BpOverview_MinorPoints);
                TextSettingApply(window.RatioChar, setting.TextSettings.BpOverview_MinorPoints);
            }

            if (!isInitial)
                _messageBoxService.ShowInfoAsync("部分设置重启后生效");
        }

        /// <summary>
        /// 文本样式应用
        /// </summary>
        /// <param name="textBlock">文本控件</param>
        /// <param name="settings">文本样式</param>
        private static void TextSettingApply(TextBlock textBlock, TextSettings settings)
        {
            textBlock.Foreground = settings.ColorBrush;
            textBlock.FontFamily = settings.FontFamily;
            textBlock.FontSize = settings.FontSize;
        }

        /// <summary>
        /// 应用所有窗口设置
        /// </summary>
        public void ApplyAllWindowsSettings()
        {
            foreach (var window in _frontWindows)
            {
                ApplySettings(window.Key, true);
            }
        }

        #endregion
    }
}