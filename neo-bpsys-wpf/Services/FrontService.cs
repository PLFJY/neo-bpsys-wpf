using neo_bpsys_wpf.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

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

        public FrontService(BpWindow bpWindow, InterludeWindow interludeWindow, GameDataWindow gameDataWindow, ScoreWindow scoreWindow, WidgetsWindow widgetsWindow)
        {
            _bpWindow = bpWindow;
            _interludeWindow = interludeWindow;
            _gameDataWindow = gameDataWindow;
            _scoreWindow = scoreWindow;
            _widgetsWindow = widgetsWindow;
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

    }
}
