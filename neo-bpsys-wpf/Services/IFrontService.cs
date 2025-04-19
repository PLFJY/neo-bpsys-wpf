using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace neo_bpsys_wpf.Services
{
    public interface IFrontService
    {
        public void AllWindowShow();
        public void AllWindowHide();
        public void BpWindowShow();
        public void BpWindowHide();
        public void InterludeWindowShow();
        public void InterludeWindowHide();
        public void GameDataWindowShow();
        public void GameDataWindowHide();
        public void ScoreWindowShow();
        public void ScoreWindowHide();
        public void WidgetsWindowShow();
        public void WidgetsWindowHide();
        public void RecordInitialPositions(Window window);
        public string GetWindowElementsPosition(Window window);
        public void LoadWindowElementsPosition(Window window,string json);
        public void RestoreInitialPositions(Window window);
    }
}
