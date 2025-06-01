using System.Windows;

namespace neo_bpsys_wpf.Services
{
    public interface IFrontService
    {
        Dictionary<Type, bool> FrontWindowStates { get; }
        Task LoadWindowElementsPositionAsync<T>(string canvasName = "BaseCanvas") where T : Window;
        void AllWindowHide();
        void AllWindowShow();
        void ShowWindow<T>() where T : Window;
        void HideWindow<T>() where T : Window;
        void RegisterFrontWindowAndCanvas(Window window, string canvasName = "BaseCanvas");
        void RestoreInitialPositions<T>(string canvasName = "BaseCanvas") where T : Window;
        void SaveWindowElementsPosition<T>(string canvasName = "BaseCanvas") where T : Window;
    }
}