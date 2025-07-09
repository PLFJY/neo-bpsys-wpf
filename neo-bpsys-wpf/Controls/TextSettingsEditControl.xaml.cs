using ColorPicker.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace neo_bpsys_wpf.Controls
{
    /// <summary>
    /// TextSettingsEditControl.xaml 的交互逻辑
    /// </summary>
    [ObservableObject]
    public partial class TextSettingsEditControl : UserControl
    {
        private readonly TextSettings? _textSettings;
        private readonly Action _applyAction;
        private readonly Action _saveAction;
        private readonly Action _closeAction;

        public TextSettingsEditControl(List<FontFamily> fontList, TextSettings? textSettings, Action applyAction,
            Action saveAction, Action cancelAction)
        {
            InitializeComponent();
            DataContext = this;
            FontList = fontList;
            _textSettings = textSettings;
            _applyAction = applyAction;
            _saveAction = saveAction;
            _closeAction = cancelAction;
            if(textSettings == null) return;
            SelectedColor = textSettings.Color.ToColor();
            SelectedFontFamily = textSettings.FontFamily;
            SelectedFontSize = textSettings.FontSize.ToString();
        }

        [ObservableProperty]
        private List<FontFamily> _fontList;

        [ObservableProperty]
        private Color _selectedColor = Color.FromArgb(255, 255, 255, 255);

        [ObservableProperty]
        private FontFamily _selectedFontFamily = new("Arial");

        [ObservableProperty]
        private string _selectedFontSize = "16.0";

        [RelayCommand]
        private void Apply()
        {
            if (_textSettings == null)
            {
                _closeAction?.Invoke();
                return;
            }

            if(double.TryParse(SelectedFontSize, out var fontsize))
                _textSettings.FontSize = fontsize;
            _textSettings.FontFamily = SelectedFontFamily;
            _textSettings.Color = SelectedColor.ToArgbHexString();
            _applyAction.Invoke();
        }

        [RelayCommand]
        private void Save()
        {
            if (_textSettings == null)
            {
                _closeAction?.Invoke();
                return;
            }

            if(double.TryParse(SelectedFontSize, out var fontsize))
                _textSettings.FontSize = fontsize;
            _textSettings.FontFamily = SelectedFontFamily;
            _textSettings.Color = SelectedColor.ToArgbHexString();
            _saveAction?.Invoke();
        }

        [RelayCommand]
        private void Close()
        {
            _closeAction?.Invoke();
        }
    }
}