using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Views.Windows;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 窗口服务类，负责管理系统中所有窗口的显示状态管理和元素位置持久化
    /// </summary>
    public class FrontService : IFrontService
    {
        // 存储窗口实例的字典（窗口类型 -> 窗口实例）
        private readonly Dictionary<Type, Window> _frontWindows = new();

        // 存储窗口显示状态的字典（窗口类型 -> 是否显示）
        private readonly Dictionary<Type, bool> _frontWindowStates = new();

        // 各窗口运行状态属性
        public bool IsBpWindowRunning { get; set; } = false;
        public bool IsInterludeWindowRunning { get; set; } = false;
        public bool IsGameDataWindowRunning { get; set; } = false;
        public bool IsScoreWindowRunning { get; set; } = false;
        public bool IsWidgetsWindowRunning { get; set; } = false;

        // 存储XAML中定义的初始位置（窗口类型-画布名称 -> 元素名称 -> 初始坐标）
        private readonly Dictionary<
            string,
            Dictionary<string, (double left, double top)>
        > _elementsInitialPositions = new();

        /// <summary>
        /// 索引器获取/设置指定窗口类型的显示状态
        /// </summary>
        /// <param name="windowType">窗口类型</param>
        /// <returns>窗口当前显示状态</returns>
        public bool this[Type windowType]
        {
            get => _frontWindowStates.TryGetValue(windowType, out var state) ? state : false;
            set => _frontWindowStates[windowType] = value;
        }

        /// <summary>
        /// 构造函数，初始化窗口服务并记录各窗口初始元素位置
        /// </summary>
        /// <param name="bpWindow">BP窗口实例</param>
        /// <param name="interludeWindow">间奏窗口实例</param>
        /// <param name="gameDataWindow">游戏数据窗口实例</param>
        /// <param name="scoreWindow">得分窗口实例</param>
        /// <param name="widgetsWindow">小部件窗口实例</param>
        public FrontService(
            BpWindow bpWindow,
            InterludeWindow interludeWindow,
            GameDataWindow gameDataWindow,
            ScoreWindow scoreWindow,
            WidgetsWindow widgetsWindow
        )
        {
            // 注册窗口实例到字典
            _frontWindows[typeof(BpWindow)] = bpWindow;
            _frontWindows[typeof(InterludeWindow)] = interludeWindow;
            _frontWindows[typeof(GameDataWindow)] = gameDataWindow;
            _frontWindows[typeof(ScoreWindow)] = scoreWindow;
            _frontWindows[typeof(WidgetsWindow)] = widgetsWindow;

            // 记录各窗口画布的初始元素位置
            RecordInitialPositions(_frontWindows[typeof(BpWindow)]);
            RecordInitialPositions(_frontWindows[typeof(InterludeWindow)]);
            RecordInitialPositions(_frontWindows[typeof(ScoreWindow)], "ScoreSurCanvas");
            RecordInitialPositions(_frontWindows[typeof(ScoreWindow)], "ScoreHunCanvas");
            RecordInitialPositions(_frontWindows[typeof(ScoreWindow)], "ScoreGlobalCanvas");
            RecordInitialPositions(_frontWindows[typeof(WidgetsWindow)], "MapBpCanvas");
        }

        /// <summary>
        /// 显示所有已注册窗口
        /// </summary>
        public void AllWindowShow()
        {
            foreach (var window in _frontWindows.Values)
            {
                window.Show();
                _frontWindowStates[window.GetType()] = true;
            }
        }

        /// <summary>
        /// 隐藏所有已注册窗口
        /// </summary>
        public void AllWindowHide()
        {
            foreach (var window in _frontWindows.Values)
            {
                window.Hide();
                _frontWindowStates[window.GetType()] = false;
            }
        }

        /// <summary>
        /// 显示指定类型的窗口
        /// </summary>
        /// <typeparam name="T">要显示的窗口类型</typeparam>
        /// <exception cref="ArgumentException">当指定类型未注册时抛出</exception>
        public void ShowWindow<T>() where T : Window
        {
            if (!_frontWindows.TryGetValue(typeof(T), out var window))
                throw new ArgumentException($"未注册的窗口类型：{typeof(T)}");

            window.Show();
            _frontWindowStates[typeof(T)] = true;
        }

        /// <summary>
        /// 隐藏指定类型的窗口
        /// </summary>
        /// <typeparam name="T">要隐藏的窗口类型</typeparam>
        /// <exception cref="ArgumentException">当指定类型未注册时抛出</exception>
        public void HideWindow<T>() where T : Window
        {
            if (!_frontWindows.TryGetValue(typeof(T), out var window))
                throw new ArgumentException($"未注册的窗口类型：{typeof(T)}");

            window.Hide();
            _frontWindowStates[typeof(T)] = false;
        }

        /// <summary>
        /// 记录窗口中指定画布的子元素初始位置
        /// </summary>
        /// <param name="window">目标窗口实例</param>
        /// <param name="canvasName">画布名称（默认BaseCanvas）</param>
        private void RecordInitialPositions(Window window, string canvasName = "BaseCanvas")
        {
            var canvas = window.FindName(canvasName) as Canvas;
            if (canvas == null)
                return;

            _elementsInitialPositions[$"{window.GetType().Name}-{canvasName}"] = new();
            _elementsInitialPositions[$"{window.GetType().Name}-{canvasName}"].Clear();
            foreach (UIElement child in canvas.Children)
            {
                if (child is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
                {
                    _elementsInitialPositions[$"{window.GetType().Name}-{canvasName}"][fe.Name] = (
                        Canvas.GetLeft(fe),
                        Canvas.GetTop(fe)
                    );
                }
            }
        }

        /// <summary>
        /// 获取窗口中指定画布的元素位置信息并序列化为JSON
        /// </summary>
        /// <param name="window">目标窗口实例</param>
        /// <param name="canvasName">画布名称（默认BaseCanvas）</param>
        /// <returns>包含位置信息的JSON字符串</returns>
        public string GetWindowElementsPosition(Window window, string canvasName = "BaseCanvas")
        {
            var position = new List<PositionInfo>();
            var canvas = window.FindName(canvasName) as Canvas;
            if (canvas != null)
            {
                foreach (var child in canvas.Children)
                {
                    if (child is FrameworkElement element)
                    {
                        var name = element.Name;
                        var left = Canvas.GetLeft(element);
                        if (double.IsNaN(left))
                            left = 0;
                        var top = Canvas.GetTop(element);
                        if (double.IsNaN(top))
                            top = 0;
                        position.Add(new PositionInfo(name, left, top));
                    }
                }
            }
            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(position, options);
        }

        /// <summary>
        /// 从JSON加载元素位置信息并应用到窗口元素
        /// </summary>
        /// <param name="window">目标窗口实例</param>
        /// <param name="json">包含位置信息的JSON字符串</param>
        /// <param name="canvasName">画布名称（默认BaseCanvas）</param>
        public void LoadWindowElementsPosition(Window window, string json, string canvasName = "BaseCanvas")
        {
            var positions = JsonSerializer.Deserialize<List<PositionInfo>>(json);
            if (window.FindName(canvasName) is Canvas canvas && positions != null)
            {
                var positionMap = positions.ToDictionary(
                    p => p.Name,
                    p => (p.Left, p.Top),
                    StringComparer.OrdinalIgnoreCase
                );
                foreach (UIElement child in canvas.Children)
                {
                    if (child is FrameworkElement fe && positionMap.ContainsKey(fe.Name))
                    {
                        var (left, top) = positionMap[fe.Name];
                        Canvas.SetLeft(fe, left);
                        Canvas.SetTop(fe, top);
                    }
                }
            }
        }

        /// <summary>
        /// 将窗口元素恢复到初始记录的位置
        /// </summary>
        /// <param name="window">目标窗口实例</param>
        /// <param name="canvasName">画布名称（默认BaseCanvas）</param>
        public void RestoreInitialPositions(Window window, string canvasName = "BaseCanvas")
        {
            var canvas = window.FindName(canvasName) as Canvas;
            if (canvas == null || _elementsInitialPositions[$"{window.GetType().Name}-{canvasName}"].Count == 0)
                return;

            foreach (UIElement child in canvas.Children)
            {
                if (
                    child is FrameworkElement fe
                    && _elementsInitialPositions[$"{window.GetType().Name}-{canvasName}"].ContainsKey(fe.Name)
                )
                {
                    var (left, top) = _elementsInitialPositions[$"{window.GetType().Name}-{canvasName}"][fe.Name];
                    Canvas.SetLeft(fe, left);
                    Canvas.SetTop(fe, top);
                }
            }
        }

        /// <summary>
        /// 元素位置信息数据类
        /// </summary>
        /// <param name="name">元素名称</param>
        /// <param name="left">左侧坐标</param>
        /// <param name="top">顶部坐标</param>
        public class PositionInfo(string name, double left, double top)
        {
            public string Name { get; set; } = name;
            public double Left { get; set; } = left;
            public double Top { get; set; } = top;
        }

        /// <summary>
        /// 窗口分辨率数据类
        /// </summary>
        /// <param name="width">窗口宽度</param>
        /// <param name="height">窗口高度</param>
        public class WindowResolution(int width, int height)
        {
            public int Width { get; set; } = width;
            public int Height { get; set; } = height;
        }
    }
}