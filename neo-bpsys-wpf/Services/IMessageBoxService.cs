namespace neo_bpsys_wpf.Services
{
    public interface IMessageBoxService
    {
        /// <summary>
        /// 显示确认对话框
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="primaryButtonText"></param>
        /// <param name="secondaryButtonText"></param>
        /// <returns></returns>
        Task<bool> ShowDeleteConfirmAsync(string title, string message, string primaryButtonText = "确认", string secondaryButtonText = "取消");
        /// <summary>
        /// 显示错误对话框
        /// </summary>
        /// <param name="message"></param>
        /// <param name="title"></param>
        /// <param name="closeButtonText"></param>
        /// <returns></returns>
        Task ShowErrorAsync(string message, string title = "错误", string closeButtonText = "关闭");
        /// <summary>
        /// 显示提示对话框
        /// </summary>
        /// <param name="message"></param>
        /// <param name="title"></param>
        /// <param name="closeButtonText"></param>
        /// <returns></returns>
        Task ShowInfoAsync(string message, string title = "提示", string closeButtonText = "关闭");
        /// <summary>
        /// 显示确认对话框
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="primaryButtonText"></param>
        /// <param name="secondaryButtonText"></param>
        /// <returns></returns>
        Task<bool> ShowConfirmAsync(string title, string message, string primaryButtonText = "确认", string secondaryButtonText = "取消");
    }
}
