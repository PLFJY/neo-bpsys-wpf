using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Services
{
    public interface IInfoBarService
    {
        void SetInfoBarControl(InfoBar infoBar);
        void ShowErrorInfoBar(string message);
        void ShowInformationalInfoBar(string message);
        void ShowSuccessInfoBar(string message);
        void ShowWarningInfoBar(string message);
    }
}
