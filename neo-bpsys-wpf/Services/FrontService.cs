using neo_bpsys_wpf.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace neo_bpsys_wpf.Services
{
    public class FrontService : IFrontService
    {
        private readonly BpWindow _bpWindow;
        private readonly InterludeWindow _interludeWindow;
        private readonly ScoreWindow _scoreWindow;
        private readonly GameDataWindow _gameDataWindow;
        private readonly WidgetsWindow _widgetsWindow;

        public bool IsBpWindowRunning { get; set; } = false;
        public bool IsInterludeWindowRunning { get; set; } = false;
        public bool IsGameDataWindowRunning { get; set; } = false;
        public bool IsScoreWindowRunning { get; set; } = false;
        public bool IsWidgetsWindowRunning { get; set; } = false;

        // 存储XAML中定义的初始位置（控件名称 -> 初始坐标）
        private readonly Dictionary<Window, Dictionary<string, (double left, double top)>> _elementsInitialPositions = new();

        public FrontService(BpWindow bpWindow, InterludeWindow interludeWindow, GameDataWindow gameDataWindow, ScoreWindow scoreWindow, WidgetsWindow widgetsWindow)
        {
            _bpWindow = bpWindow;
            _interludeWindow = interludeWindow;
            _gameDataWindow = gameDataWindow;
            _scoreWindow = scoreWindow;
            _widgetsWindow = widgetsWindow;

            RecordInitialPositions(_bpWindow);
        }
        //窗口显示/隐藏管理
        public void AllWindowShow()
        {
            BpWindowShow();
            InterludeWindowShow();
            GameDataWindowShow();
            ScoreWindowShow();
            WidgetsWindowShow();
        }
        public void AllWindowHide()
        {
            BpWindowHide();
            InterludeWindowHide();
            GameDataWindowHide();
            ScoreWindowHide();
            WidgetsWindowHide();
        }

        public void BpWindowShow()
        {
            _bpWindow.Show();
            IsBpWindowRunning = true;
        }
        public void BpWindowHide()
        {
            _bpWindow.Hide();
            IsBpWindowRunning = false;
        }

        public void InterludeWindowShow()
        {
            _interludeWindow.Show();
            IsInterludeWindowRunning = true;
        }
        public void InterludeWindowHide()
        {
            _interludeWindow.Hide();
            IsInterludeWindowRunning = false;
        }
        public void GameDataWindowShow()
        {
            _gameDataWindow.Show();
            IsGameDataWindowRunning = true;
        }

        public void GameDataWindowHide()
        {
            _gameDataWindow.Hide();
            IsGameDataWindowRunning = false;
        }

        public void ScoreWindowShow()
        {
            _scoreWindow.Show();
            IsScoreWindowRunning = true;
        }

        public void ScoreWindowHide()
        {
            _scoreWindow.Hide();
            IsScoreWindowRunning = false;
        }

        public void WidgetsWindowShow()
        {
            _widgetsWindow.Show();
            IsWidgetsWindowRunning = true;
        }

        public void WidgetsWindowHide()
        {
            _widgetsWindow.Hide();
            IsWidgetsWindowRunning = false;
        }

        /// <summary>
        /// 记录窗口中元素的初始位置
        /// </summary>
        /// <param name="window">该窗口的实例</param>
        public void RecordInitialPositions(Window window)
        {
            var canvas = window.FindName("BaseCanvas") as Canvas;
            if (canvas == null) return;

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
                        if(double.IsNaN(left)) left = 0;
                        var top = Canvas.GetTop(element);
                        position.Add(new PositionInfo(name, left, top));
                    }
                }
            }
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
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
                var positionMap = positions.ToDictionary(p => p.Name, p => (p.Left, p.Top), StringComparer.OrdinalIgnoreCase);
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
            if (canvas == null || _elementsInitialPositions[window].Count == 0) return;

            foreach (UIElement child in canvas.Children)
            {
                if (child is FrameworkElement fe && _elementsInitialPositions[window].ContainsKey(fe.Name))
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
