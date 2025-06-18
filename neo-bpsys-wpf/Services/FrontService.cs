using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.AttachedBehaviors;
using neo_bpsys_wpf.CustomControls;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Views.Windows;
using Path = System.IO.Path;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 前台窗口服务, 实现了 <see cref="IFrontService"/> 接口，负责与前台窗口进行交互
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

        public FrontService(
            BpWindow bpWindow,
            InterludeWindow interludeWindow,
            GameDataWindow gameDataWindow,
            ScoreWindow scoreWindow,
            WidgetsWindow widgetsWindow,
            IMessageBoxService messageBoxService,
            ISharedDataService sharedDataService
        )
        {
            _messageBoxService = messageBoxService;
            var savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf");
            if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);
            // 注册窗口和画布
            RegisterFrontWindowAndCanvas(bpWindow);
            RegisterFrontWindowAndCanvas(interludeWindow);
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
            WeakReferenceMessenger.Default.Register<PropertyChangedMessage<double>>(this, DoublePropertyChangedRecipient);
            OnBo3ModeChanged();
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
            foreach (var window in _frontWindows.Values)
            {
                if (FrontWindowStates[window.GetType()]) continue;
                window.Show();
                FrontWindowStates[window.GetType()] = true;
            }
        }

        public void AllWindowHide()
        {
            foreach (var window in _frontWindows.Values)
            {
                if (!FrontWindowStates[window.GetType()]) continue;
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
        /// <exception cref="ArgumentException"></exception>
        private static void RegisterControl(string nameHeader, GameProgress key, Dictionary<GameProgress, FrameworkElement> elementDict, FrameworkElement control, bool isOverride = true)
        {
            var name = nameHeader + key.ToString();
            control.Name = name;
            if (elementDict.TryAdd(key, control)) return;
            if (!isOverride)
                throw new ArgumentException($"Control with key '{key}' already exists. Set isOverride to true to replace.");
            else
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
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.default.json");

            if (File.Exists(path)) return;

            var positions = GetElementsPositions(window, canvasName);
            if(positions == null) return;
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
                    double.IsNaN(Canvas.GetLeft(fe)) ? null : Canvas.GetLeft(fe),
                    double.IsNaN(Canvas.GetTop(fe)) ? null : Canvas.GetTop(fe));
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
            if(positions == null) return;

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.json");
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

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.json");
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
        private static void LoadElementsPositions<T>(string canvasName, string jsonContent, Window window) where T : Window
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

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.default.json");
            var sourceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Resources\\FrontDefalutPositions", $"{window.GetType().Name}Config-{canvasName}.default.json");
#if !DEBUG
            if (!File.Exists(path) && File.Exists(sourceFilePath))
            {
                try
                {
                    File.Copy(sourceFilePath, path, true);
                }
                catch (Exception ex)
                {
                    await _messageBoxService.ShowInfoAsync($"前台默认配置复制失败\n{ex.Message}", "复制提示");
                }
            }
#endif
            try
            {
                var json = await File.ReadAllTextAsync(path);
                LoadElementsPositions<T>(canvasName, json, window);

                var customFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.json");
                if (File.Exists(customFilePath))
                    File.Move(customFilePath, customFilePath + ".disabled", true);
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
            foreach (GameProgress progress in Enum.GetValues<GameProgress>())
            {
                if (progress == GameProgress.Free) continue;
                var control = new GlobalScorePresenter();
                RegisterControl("Main", progress, MainGlobalScoreControls, control);
            }
            //客队
            foreach (GameProgress progress in Enum.GetValues<GameProgress>())
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
                foreach (var item in MainGlobalScoreControls.Where(item => item.Key > GameProgress.Game3ExtraSecondHalf))
                {
                    item.Value.Visibility = Visibility.Hidden;
                }
                foreach (var item in AwayGlobalScoreControls.Where(item => item.Key > GameProgress.Game3ExtraSecondHalf))
                {
                    item.Value.Visibility = Visibility.Hidden;
                }
                Canvas.SetLeft(scoreWindow.MainScoreTotal, Canvas.GetLeft(scoreWindow.MainScoreTotal) - _globalScoreTotalMargin);
                Canvas.SetLeft(scoreWindow.AwayScoreTotal, Canvas.GetLeft(scoreWindow.AwayScoreTotal) - _globalScoreTotalMargin);
                _lastMove = _globalScoreTotalMargin;
            }
            else
            {
                scoreWindow.ScoreGlobalCanvas.Background = ImageHelper.GetUiImageBrush("scoreGlobal");
                foreach (var item in MainGlobalScoreControls.Where(item => item.Key > GameProgress.Game3ExtraSecondHalf))
                {
                    item.Value.Visibility = Visibility.Visible;
                }
                foreach (var item in AwayGlobalScoreControls.Where(item => item.Key > GameProgress.Game3ExtraSecondHalf))
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
        public void FadeInAnimation<T>(string controlNameHeader, int controlIndex, string controlNameFooter) where T : Window
        {
            var ctrName = controlNameHeader + (controlIndex >= 0 ?  controlIndex : string.Empty) + controlNameFooter;
            if (_frontWindows[typeof(T)] is not T window) return;
            
            if (window.FindName(ctrName) is FrameworkElement element)
            {
                element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5))));
            }
        }
        
        /// <summary>
        /// 渐隐动画
        /// </summary>
        /// <param name="controlNameHeader"></param>
        /// <param name="controlIndex"></param>
        /// <param name="controlNameFooter"></param>
        /// <typeparam name="T"></typeparam>
        public void FadeOutAnimation<T>(string controlNameHeader, int controlIndex, string controlNameFooter) where T : Window
        { 
            var ctrName = controlNameHeader + (controlIndex >= 0 ?  controlIndex : string.Empty) + controlNameFooter;
            if (_frontWindows[typeof(T)] is not T window) return;
            if (window.FindName(ctrName) is FrameworkElement element)
            {
                element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5))));
            }
        }

        /// <summary>
        /// 呼吸动画开始
        /// </summary>
        /// <param name="controlNameHeader"></param>
        /// <param name="controlIndex"></param>
        /// <param name="controlNameFooter"></param>
        /// <typeparam name="T"></typeparam>
        public async Task BreathingStart<T>(string controlNameHeader, int controlIndex, string controlNameFooter) where T : Window
        { 
            var ctrName = controlNameHeader + (controlIndex >= 0 ?  controlIndex : string.Empty) + controlNameFooter;
            if (_frontWindows[typeof(T)] is not T window) return;
            if (window.FindName(ctrName) is not FrameworkElement element) return;

            element.Opacity = 0;
            element.Visibility = Visibility.Visible;
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5))));
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
        public async Task BreathingStop<T>(string controlNameHeader, int controlIndex, string controlNameFooter) where T : Window
        {
            var ctrName = controlNameHeader + (controlIndex >= 0 ?  controlIndex : string.Empty) + controlNameFooter;
            if (_frontWindows[typeof(T)] is not T window) return;
            if (window.FindName(ctrName) is not FrameworkElement element) return;
            if (element.Tag is not Storyboard storyboard) return;

            storyboard.Stop();
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5))));
            await Task.Delay(500);

            element.Opacity = 0; // 恢复初始状态
            element.Tag = null;
            element.Visibility = Visibility.Hidden;
        }
        #endregion
    }
}