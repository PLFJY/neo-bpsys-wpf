using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Views.Windows;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private MainWindow mainWindow;

        [ObservableProperty] private bool _isTopmost = false;
        public MainWindowViewModel(MainWindow main)
        {
            mainWindow = main;
        }

        [RelayCommand]
        private void MiniMize()
        {
            mainWindow.WindowState = WindowState.Minimized;
        }

        [RelayCommand]
        private void Maximize()
        {
            mainWindow.WindowState =
                mainWindow.WindowState == WindowState.Maximized ?
                WindowState.Normal :
                WindowState.Maximized;
        }

        [RelayCommand]
        private async Task Close()
        {
            var uiMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                IsPrimaryButtonEnabled = true,
                Title = "关闭提示",
                Content = "是否退出程序",
                CloseButtonText = "取消",
                PrimaryButtonText = "退出",
            };

            var result = await uiMessageBox.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary) Environment.Exit(0);

        }

        [RelayCommand]
        private void ThemeChange()
        {
            ApplicationThemeManager.Apply(
                ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Light ?
                ApplicationTheme.Dark :
                ApplicationTheme.Light);
        }

        [RelayCommand]
        private void Topmost()
        {
            //mainWindow.Topmost = _isTopmost;
        }

        [RelayCommand]
        private void TitleBarMouseDown()
        {
            mainWindow.DragMove();
        }
    }
}
