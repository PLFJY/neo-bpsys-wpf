using System.Runtime.CompilerServices;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Services
{
    public class InfoBarService : IInfoBarService
    {
        private InfoBar? _infoBar;
        public void SetInfoBarControl(InfoBar infoBar)
        {
            _infoBar = infoBar;
        }

        public void ShowErrorInfoBar(string message)
        {
            if (_infoBar != null)
            {
                _infoBar.Message = message;
                _infoBar.Severity = InfoBarSeverity.Error;
                _infoBar.IsOpen = true;
            }
        }

        public void ShowInformationalInfoBar(string message)
        {
            if (_infoBar != null)
            {
                _infoBar.Message = message;
                _infoBar.Severity = InfoBarSeverity.Informational;
                _infoBar.IsOpen = true;
            }
        }

        public void ShowSuccessInfoBar(string message)
        {
            if (_infoBar != null)
            {
                _infoBar.Message = message;
                _infoBar.Severity = InfoBarSeverity.Success;
                _infoBar.IsOpen = true;
            }
        }

        public void ShowWarningInfoBar(string message)
        {
            if (_infoBar != null)
            {
                _infoBar.Message = message;
                _infoBar.Severity = InfoBarSeverity.Error;
                _infoBar.IsOpen = true;
            }
        }

        public void CloseInfoBar()
        {
            if(_infoBar != null)
            {
                _infoBar.IsOpen = false;
            }
        }
    }
}
