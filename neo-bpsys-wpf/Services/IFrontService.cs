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
        public void RecordInitialPositions(Window window);
        public string GetWindowElementsPosition(Window window);
        public void LoadWindowElementsPosition(Window window, string json);
        public void RestoreInitialPositions(Window window);
    }
}
