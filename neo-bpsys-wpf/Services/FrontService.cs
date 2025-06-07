using neo_bpsys_wpf.CustomBehaviors;
using neo_bpsys_wpf.CustomControls;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;
using System.Xml.Linq;
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

        public static readonly Dictionary<GameProgress, FrameworkElement> MainGlobalScoreControls = [];
        public static readonly Dictionary<GameProgress, FrameworkElement> AwayGlobalScoreControls = [];

        private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
        private readonly IMessageBoxService _messageBoxService;

        public FrontService(
            BpWindow bpWindow,
            InterludeWindow interludeWindow,
            GameDataWindow gameDataWindow,
            ScoreWindow scoreWindow,
            WidgetsWindow widgetsWindow,
            IMessageBoxService messageBoxService
        )
        {
            _messageBoxService = messageBoxService;
            // 注册窗口和画布
            RegisterFrontWindowAndCanvas(bpWindow);
            RegisterFrontWindowAndCanvas(interludeWindow);
            RegisterFrontWindowAndCanvas(gameDataWindow);
            RegisterFrontWindowAndCanvas(scoreWindow, "ScoreSurCanvas");
            RegisterFrontWindowAndCanvas(scoreWindow, "ScoreHunCanvas");
            RegisterFrontWindowAndCanvas(scoreWindow, "ScoreGlobalCanvas");
            RegisterFrontWindowAndCanvas(widgetsWindow, "MapBpCanvas");

            //注册分数统计界面的分数控件
            GlobalScoreContorlsReg();

            // 记录初始位置
            foreach (var i in _frontCanvas)
            {
                RecordInitialPositions(i.Item1, i.Item2);
            }
        }

        /// <summary>
        /// 注册窗口和画布
        /// </summary>
        /// <param name="window"></param>
        /// <param name="canvasName"></param>
        public void RegisterFrontWindowAndCanvas(Window window, string canvasName = "BaseCanvas")
        {
            Type type = window.GetType();

            if (!_frontWindows.ContainsKey(type))
            {
                _frontWindows[type] = window;
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
                window.Show();
                FrontWindowStates[window.GetType()] = true;
            }
        }

        public void AllWindowHide()
        {
            foreach (var window in _frontWindows.Values)
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

            window.Hide();
            FrontWindowStates[typeof(T)] = false;
        }
        #endregion 窗口显示/隐藏管理

        #region 前台动态控件添加

        /// <summary>
        /// 将控件添加到 Canvas 并设置位置
        /// </summary>
        private static void AddControlToCanvas(FrameworkElement control, Canvas canvas, GameProgress progress, int top)
        {
            // 设置控件位置（示例逻辑）
            double left = CalculateLeftPosition(progress);

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
            if (!elementDict.TryAdd(key, control))
            {
                if (!isOverride)
                    throw new ArgumentException($"Control with key '{key}' already exists. Set isOverride to true to replace.");
                else
                    elementDict[key] = control;
            }
        }

        /// <summary>
        /// 获取控件
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static FrameworkElement? GetControl(int key, Dictionary<int, FrameworkElement> elementDict)
        {
            elementDict.TryGetValue(key, out var control);
            return control;
        }
        #endregion 前台动态控件添加

        #region 设计者模式
        /// <summary>
        /// 记录窗口中元素的初始位置
        /// </summary>
        /// <param name="window">该窗口的实例</param>
        public void RecordInitialPositions(Window window, string canvasName = "BaseCanvas")
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.default.json");

            if (File.Exists(path)) return;
#if DEBUG
            if (window.FindName(canvasName) is not Canvas canvas)
                return;

            var positions = new Dictionary<string, PositionInfo>();
            foreach (UIElement child in canvas.Children)
            {
                if (child is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
                {
                    var name = fe.Name;
                    var left = Canvas.GetLeft(fe);
                    if (double.IsNaN(left))
                        left = 0;
                    var top = Canvas.GetTop(fe);
                    if (double.IsNaN(top))
                        top = 0;

                    positions[name] = new(left, top);
                }
            }
            var output = JsonSerializer.Serialize(positions, _jsonSerializerOptions);

            try
            {
                File.WriteAllText(path, output);
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowErrorAsync(ex.Message, "生成默认前台配置文件发生错误");
            }
#endif
#if !DEBUG
            try
            {
                var sourceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Resources\\FrontDefalutPositions", $"{window.GetType().Name}Config-{canvasName}.default.json");

                if (File.Exists(sourceFilePath))
                    File.Copy(sourceFilePath, path, true);
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowErrorAsync(ex.Message, "复制默认前台配置文件发生错误");
            }
#endif
        }

        /// <summary>
        /// 获取窗口中元素的位置信息
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

            if(typeof(T) == typeof(ScoreWindow) && canvasName == "ScoreGlobalCanvas" && _isBo3Mode) return;

            var positions = new Dictionary<string, PositionInfo>();
            if (window.FindName(canvasName) is Canvas canvas)
            {
                foreach (var child in canvas.Children)
                {
                    if (child is FrameworkElement element)
                    {
                        var left = Canvas.GetLeft(element);
                        if (double.IsNaN(left))
                            left = 0;
                        var top = Canvas.GetTop(element);
                        if (double.IsNaN(top))
                            top = 0;

                        positions[element.Name] = new(left, top);
                    }
                }
            }

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.json");
            try
            {
                var josnContent = JsonSerializer.Serialize(positions, _jsonSerializerOptions);
                File.WriteAllText(path, josnContent);
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowInfoAsync($"保存前台配置文件失败\n{ex.Message}", "保存提示");
            }
        }

        /// <summary>
        /// 从JSON中加载窗口中元素的位置信息
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <param name="canvasName">画布名称</param>
        public async Task LoadWindowElementsPositionAsync<T>(string canvasName = "BaseCanvas") where T : Window
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
                var jsonContent = File.ReadAllText(path);
                var positions = JsonSerializer.Deserialize<Dictionary<string, PositionInfo>>(jsonContent);

                if (window.FindName(canvasName) is Canvas canvas && positions != null)
                {
                    foreach (UIElement child in canvas.Children)
                    {
                        if (child is FrameworkElement fe && positions.TryGetValue(fe.Name, out PositionInfo? value))
                        {
                            var left = value.Left;
                            if (double.IsNaN(left))
                                left = 0;
                            var top = value.Top;
                            if (double.IsNaN(top))
                                top = 0;

                            Canvas.SetLeft(fe, left);
                            Canvas.SetTop(fe, top);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                File.Move(path, path + ".disabled", true);
                await _messageBoxService.ShowErrorAsync(ex.Message);
            }
        }

        /// <summary>
        /// 还原窗口中的元素到初始位置
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="canvasName"></param>
        public async void RestoreInitialPositions<T>(string canvasName = "BaseCanvas") where T : Window
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
                var json = File.ReadAllText(path);
                var positions = JsonSerializer.Deserialize<Dictionary<string, PositionInfo>>(json);
                if (positions == null || window.FindName(canvasName) is not Canvas canvas || positions.Count == 0) return;

                foreach (UIElement child in canvas.Children)
                {
                    if (child is FrameworkElement fe && positions.TryGetValue(fe.Name, out PositionInfo? value))
                    {
                        Canvas.SetLeft(fe, value.Left);
                        Canvas.SetTop(fe, value.Top);
                    }
                }

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
        /// 窗口中元素的位置信息
        /// </summary>
        /// <param name="name"></param>
        /// <param name="left"></param>
        /// <param name="top"></param>
        public class PositionInfo(double left, double top)
        {
            public double Left { get; set; } = left;
            public double Top { get; set; } = top;
        }
        #endregion 设计者模式

        #region 分数统计
        private bool _isBo3Mode;

        /// <summary>
        /// 注册全局计分板控件
        /// </summary>
        private void GlobalScoreContorlsReg()
        {

            if (_frontWindows[typeof(ScoreWindow)].FindName("ScoreGlobalCanvas") is Canvas canvas)
            {
                //主队
                foreach (GameProgress progress in Enum.GetValues<GameProgress>())
                {
                    if (progress != GameProgress.Free)
                    {
                        var control = new GlobalScorePresenter();
                        RegisterControl("Main", progress, MainGlobalScoreControls, control);
                    }
                }
                //客队
                foreach (GameProgress progress in Enum.GetValues<GameProgress>())
                {
                    if (progress != GameProgress.Free)
                    {
                        var control = new GlobalScorePresenter();
                        RegisterControl("Away", progress, AwayGlobalScoreControls, control);
                    }
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
        }

        /// <summary>
        /// 设置分数统计
        /// </summary>
        /// <param name="Team"></param>
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
        public double GlobalScoreTotalMargin { get; set; }

        private double _lastMove;

        /// <summary>
        /// 切换赛制到
        /// </summary>
        public void SwitchGameType(bool isBo3)
        {
            _isBo3Mode = isBo3;
            if (_frontWindows[typeof(ScoreWindow)] is not ScoreWindow scoreWindow) return;
            if (_isBo3Mode)
            {
                scoreWindow.ScoreGlobalCanvas.Background = ImageHelper.GetUiImageBrush("scoreGlobal_Bo3");
                foreach (var item in MainGlobalScoreControls)
                {
                    if(item.Key > GameProgress.Game3ExtraSecondHalf)
                    {
                        item.Value.Visibility = Visibility.Hidden;
                    }
                }
                foreach (var item in AwayGlobalScoreControls)
                {
                    if (item.Key > GameProgress.Game3ExtraSecondHalf)
                    {
                        item.Value.Visibility = Visibility.Hidden;
                    }
                }
                Canvas.SetLeft(scoreWindow.MainScoreTotal, Canvas.GetLeft(scoreWindow.MainScoreTotal) - GlobalScoreTotalMargin);
                Canvas.SetLeft(scoreWindow.AwayScoreTotal, Canvas.GetLeft(scoreWindow.AwayScoreTotal) - GlobalScoreTotalMargin);
                _lastMove = GlobalScoreTotalMargin;
            }
            else
            {
                scoreWindow.ScoreGlobalCanvas.Background = ImageHelper.GetUiImageBrush("scoreGlobal");
                foreach (var item in MainGlobalScoreControls)
                {
                    if (item.Key > GameProgress.Game3ExtraSecondHalf)
                    {
                        item.Value.Visibility = Visibility.Visible;
                    }
                }
                foreach (var item in AwayGlobalScoreControls)
                {
                    if (item.Key > GameProgress.Game3ExtraSecondHalf)
                    {
                        item.Value.Visibility = Visibility.Visible;
                    }
                }
                Canvas.SetLeft(scoreWindow.MainScoreTotal, Canvas.GetLeft(scoreWindow.MainScoreTotal) + _lastMove);
                Canvas.SetLeft(scoreWindow.AwayScoreTotal, Canvas.GetLeft(scoreWindow.AwayScoreTotal) + _lastMove);
            }
        }
        #endregion 分数统计

        /// <summary>
        /// 窗口分辨率
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public class WindowResolution(int width, int height)
        {
            public int Width { get; set; } = width;
            public int Height { get; set; } = height;
        }
    }
}