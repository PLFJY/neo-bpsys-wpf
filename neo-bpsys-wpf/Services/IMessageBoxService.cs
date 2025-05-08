namespace neo_bpsys_wpf.Services
{
    public interface IMessageBoxService
    {
        public Task<bool> ShowDeleteConfirmAsync(
            string title,
            string message,
            string primaryButtonText = "确认",
            string secondaryButtonText = "取消"
        );
        public Task<bool> ShowExitConfirmAsync(
            string title,
            string message,
            string primaryButtonText = "退出",
            string secondaryButtonText = "取消"
        );
        public Task ShowWarningAsync(
            string message, 
            string title = "警告", 
            string closeButtonText = "关闭");
        public Task ShowInfoAsync(
           string message,
           string title = "提示",
           string closeButtonText = "关闭");
    }
}
