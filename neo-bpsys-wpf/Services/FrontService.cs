using neo_bpsys_wpf.Views.Windows;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Xml.Linq;

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

            // 记录初始位置
            foreach (var i in _frontCanvas)
            {
                RecordInitialPositions(i.Item1, i.Item2);
            }
        }

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

        //窗口显示/隐藏管理
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

        /// <summary>
        /// 记录窗口中元素的初始位置
        /// </summary>
        /// <param name="window">该窗口的实例</param>
        private void RecordInitialPositions(Window window, string canvasName = "BaseCanvas")
        {
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.default.json");

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
                var sourceFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Resources\\FrontDefalutPositions", $"{window.GetType().Name}Config-{canvasName}.default.json");

                if (File.Exists(sourceFilePath))
                    File.Copy(sourceFilePath, path);
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

            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.json");
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

            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.json");
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
                File.Move(path, path + ".disabled");
                await _messageBoxService.ShowErrorAsync(ex.Message);
            }
        }

        /// <summary>
        /// 还原窗口中的元素到初始位置
        /// </summary>
        /// <param name="window">该窗口的实例</param>
        public void RestoreInitialPositions<T>(string canvasName = "BaseCanvas") where T : Window
        {
            if (!_frontWindows.TryGetValue(typeof(T), out var window))
            {
                _messageBoxService.ShowErrorAsync($"未注册的窗口类型：{typeof(T)}", "前台默认配置恢复错误");
                return;
            }

            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.default.json");
            var sourceFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Resources\\FrontDefalutPositions", $"{window.GetType().Name}Config-{canvasName}.default.json");

            if (!File.Exists(path) && File.Exists(sourceFilePath))
            {
                try
                {
                    File.Copy(sourceFilePath, path);
                }
                catch (Exception ex)
                {
                    _messageBoxService.ShowInfoAsync($"前台默认配置复制失败\n{ex.Message}", "复制提示");
                }
            }

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

                var customFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", $"{window.GetType().Name}Config-{canvasName}.json");
                File.Move(customFilePath, customFilePath + ".disabled");
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowErrorAsync(ex.Message, "读取前台默认配置错误");
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
