using System.Windows;

namespace neo_bpsys_wpf.Services
{
    public interface IFrontService
    {
        public void AllWindowShow();
        public void AllWindowHide();
        public void ShowWindow<T>()
            where T : Window;
        public void HideWindow<T>()
            where T : Window;
        public string GetWindowElementsPosition(Window window, string canvasName = "BaseCanvas");
        public void LoadWindowElementsPosition(Window window, string json, string canvasName = "BaseCanvas");
        public void RestoreInitialPositions(Window window, string canvasName = "BaseCanvas");
    }
}
