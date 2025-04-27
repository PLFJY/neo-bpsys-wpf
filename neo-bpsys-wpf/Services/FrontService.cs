using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Views.Windows;

namespace neo_bpsys_wpf.Services
{
    public class FrontService : IFrontService
    {
        private readonly Dictionary<Type, Window> _frontWindows = new();

        private readonly Dictionary<Type, bool> _frontWindowStates = new();

        public bool IsBpWindowRunning { get; set; } = false;
        public bool IsInterludeWindowRunning { get; set; } = false;
        public bool IsGameDataWindowRunning { get; set; } = false;
        public bool IsScoreWindowRunning { get; set; } = false;
        public bool IsWidgetsWindowRunning { get; set; } = false;

        // 存储XAML中定义的初始位置（控件名称 -> 初始坐标）
        private readonly Dictionary<
            Window,
            Dictionary<string, (double left, double top)>
        > _elementsInitialPositions = new();

        public bool this[Type windowType]
        {
            get => _frontWindowStates.TryGetValue(windowType, out var state) ? state : false;
            set => _frontWindowStates[windowType] = value;
        }

        public FrontService(
            BpWindow bpWindow,
            InterludeWindow interludeWindow,
            GameDataWindow gameDataWindow,
            ScoreWindow scoreWindow,
            WidgetsWindow widgetsWindow
        )
        {
            _frontWindows[typeof(BpWindow)] = bpWindow;
            _frontWindows[typeof(InterludeWindow)] = interludeWindow;
            _frontWindows[typeof(GameDataWindow)] = gameDataWindow;
            _frontWindows[typeof(ScoreWindow)] = scoreWindow;
            _frontWindows[typeof(WidgetsWindow)] = widgetsWindow;

            RecordInitialPositions(_frontWindows[typeof(BpWindow)]);
        }

        //窗口显示/隐藏管理
        public void AllWindowShow()
        {
            foreach (var window in _frontWindows.Values)
            {
                window.Show();
                _frontWindowStates[window.GetType()] = true;
            }
        }

        public void AllWindowHide()
        {
            foreach (var window in _frontWindows.Values)
            {
                window.Hide();
                _frontWindowStates[window.GetType()] = false;
            }
        }

        public void ShowWindow<T>()
            where T : Window
        {
            if (!_frontWindows.TryGetValue(typeof(T), out var window))
                throw new ArgumentException($"未注册的窗口类型：{typeof(T)}");

            window.Show();
            _frontWindowStates[typeof(T)] = true;
        }

        public void HideWindow<T>()
            where T : Window
        {
            if (!_frontWindows.TryGetValue(typeof(T), out var window))
                throw new ArgumentException($"未注册的窗口类型：{typeof(T)}");

            window.Hide();
            _frontWindowStates[typeof(T)] = false;
        }

        /// <summary>
        /// 记录窗口中元素的初始位置
        /// </summary>
        /// <param name="window">该窗口的实例</param>
        public void RecordInitialPositions(Window window)
        {
            var canvas = window.FindName("BaseCanvas") as Canvas;
            if (canvas == null)
                return;

            _elementsInitialPositions[window] = new();
            _elementsInitialPositions[window].Clear();
            foreach (UIElement child in canvas.Children)
            {
                if (child is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
                {
                    _elementsInitialPositions[window][fe.Name] = (
                        Canvas.GetLeft(fe),
                        Canvas.GetTop(fe)
                    );
                }
            }
        }

        /// <summary>
        /// 获取窗口中元素的位置信息
        /// </summary>
        /// <param name="window">该窗口的实例</param>
        /// <returns></returns>
        public string GetWindowElementsPosition(Window window)
        {
            var position = new List<PositionInfo>();
            var canvas = window.FindName("BaseCanvas") as Canvas;
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
                        position.Add(new PositionInfo(name, left, top));
                    }
                }
            }
            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(position, options);
        }

        /// <summary>
        /// 从JSON中加载窗口中元素的位置信息
        /// </summary>
        /// <param name="window">该窗口的实例</param>
        /// <param name="json">记录有位置信息的Json原文件内容</param>
        public void LoadWindowElementsPosition(Window window, string json)
        {
            var positions = JsonSerializer.Deserialize<List<PositionInfo>>(json);
            if (window.FindName("BaseCanvas") is Canvas canvas && positions != null)
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
        /// 还原窗口中的元素到初始位置
        /// </summary>
        /// <param name="window">该窗口的实例</param>
        public void RestoreInitialPositions(Window window)
        {
            var canvas = window.FindName("BaseCanvas") as Canvas;
            if (canvas == null || _elementsInitialPositions[window].Count == 0)
                return;

            foreach (UIElement child in canvas.Children)
            {
                if (
                    child is FrameworkElement fe
                    && _elementsInitialPositions[window].ContainsKey(fe.Name)
                )
                {
                    var (left, top) = _elementsInitialPositions[window][fe.Name];
                    Canvas.SetLeft(fe, left);
                    Canvas.SetTop(fe, top);
                }
            }
        }

        /// <summary>
        /// 窗口中元素的位置信息
        /// </summary>
        /// <param name="name"></param>
        /// <param name="left"></param>
        /// <param name="top"></param>
        public class PositionInfo(string name, double left, double top)
        {
            public string Name { get; set; } = name;
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
