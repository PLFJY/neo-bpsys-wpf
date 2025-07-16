using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Abstractions.Services;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 提示框服务, 实现了 <see cref="IInfoBarService"/> 接口，负责显示提示框
    /// </summary>
    public class InfoBarService : IInfoBarService
    {
        private readonly ILogger<InfoBarService> _logger;
        private InfoBar? _infoBar;

        public InfoBarService(ILogger<InfoBarService> logger)
        {
            _logger = logger;
            _logger.LogInformation("InfoBarService initialized");
        }

        /// <summary>
        /// 设置提示框控件
        /// </summary>
        /// <param name="infoBar"></param>
        public void SetInfoBarControl(InfoBar infoBar)
        {
            _logger.LogInformation("Setting InfoBar control reference");
            _infoBar = infoBar;
        }

        /// <summary>
        /// 显示错误提示框
        /// </summary>
        /// <param name="message"></param>
        public void ShowErrorInfoBar(string message)
        {
            if (_infoBar == null)
            {
                _logger.LogWarning("ShowErrorInfoBar skipped: InfoBar control not set");
                return;
            }

            _logger.LogError("Showing error InfoBar: {Message}", message);
            _infoBar.Message = message;
            _infoBar.Severity = InfoBarSeverity.Error;
            _infoBar.IsOpen = true;
        }

        /// <summary>
        /// 显示信息提示框
        /// </summary>
        /// <param name="message"></param>
        public void ShowInformationalInfoBar(string message)
        {
            if (_infoBar == null)
            {
                _logger.LogWarning("ShowInformationalInfoBar skipped: InfoBar control not set");
                return;
            }

            _logger.LogInformation("Showing informational InfoBar: {Message}", message);
            _infoBar.Message = message;
            _infoBar.Severity = InfoBarSeverity.Informational;
            _infoBar.IsOpen = true;
        }

        /// <summary>
        /// 显示成功提示框
        /// </summary>
        /// <param name="message"></param>
        public void ShowSuccessInfoBar(string message)
        {
            if (_infoBar == null)
            {
                _logger.LogWarning("ShowSuccessInfoBar skipped: InfoBar control not set");
                return;
            }

            _logger.LogInformation("Showing success InfoBar: {Message}", message);
            _infoBar.Message = message;
            _infoBar.Severity = InfoBarSeverity.Success;
            _infoBar.IsOpen = true;
        }

        /// <summary>
        /// 显示警告提示框
        /// </summary>
        /// <param name="message"></param>
        public void ShowWarningInfoBar(string message)
        {
            if (_infoBar == null)
            {
                _logger.LogWarning("ShowWarningInfoBar skipped: InfoBar control not set");
                return;
            }

            _logger.LogWarning("Showing warning InfoBar: {Message}", message);
            _infoBar.Message = message;
            _infoBar.Severity = InfoBarSeverity.Warning;  // Corrected severity
            _infoBar.IsOpen = true;
        }

        /// <summary>
        /// 关闭提示框
        /// </summary>
        public void CloseInfoBar()
        {
            if (_infoBar == null)
            {
                _logger.LogDebug("CloseInfoBar skipped: InfoBar control not set");
                return;
            }

            _logger.LogDebug("Closing InfoBar");
            _infoBar.IsOpen = false;
        }
    }
}