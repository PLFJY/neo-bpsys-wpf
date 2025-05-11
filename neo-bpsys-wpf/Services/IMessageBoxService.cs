namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 定义消息框服务接口，提供异步显示各类消息对话框的功能
    /// </summary>
    public interface IMessageBoxService
    {
        /// <summary>
        /// 显示删除确认对话框
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="message">显示的消息内容</param>
        /// <param name="primaryButtonText">确认按钮文本（默认："确认"）</param>
        /// <param name="secondaryButtonText">取消按钮文本（默认："取消"）</param>
        /// <returns>Task<bool> 返回用户选择结果，true表示确认，false表示取消</returns>
        public Task<bool> ShowDeleteConfirmAsync(
            string title,
            string message,
            string primaryButtonText = "确认",
            string secondaryButtonText = "取消"
        );

        /// <summary>
        /// 显示退出确认对话框
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="message">显示的消息内容</param>
        /// <param name="primaryButtonText">退出按钮文本（默认："退出"）</param>
        /// <param name="secondaryButtonText">取消按钮文本（默认："取消"）</param>
        /// <returns>Task<bool> 返回用户选择结果，true表示退出，false表示取消</returns>
        public Task<bool> ShowExitConfirmAsync(
            string title,
            string message,
            string primaryButtonText = "退出",
            string secondaryButtonText = "取消"
        );

        /// <summary>
        /// 显示警告消息对话框
        /// </summary>
        /// <param name="message">显示的消息内容</param>
        /// <param name="title">对话框标题（默认："警告"）</param>
        /// <param name="closeButtonText">关闭按钮文本（默认："关闭"）</param>
        /// <returns>Task 表示异步显示操作的完成状态</returns>
        public Task ShowWarningAsync(
            string message,
            string title = "警告",
            string closeButtonText = "关闭");

        /// <summary>
        /// 显示信息提示对话框
        /// </summary>
        /// <param name="message">显示的消息内容</param>
        /// <param name="title">对话框标题（默认："提示"）</param>
        /// <param name="closeButtonText">关闭按钮文本（默认："关闭"）</param>
        /// <returns>Task 表示异步显示操作的完成状态</returns>
        public Task ShowInfoAsync(
           string message,
           string title = "提示",
           string closeButtonText = "关闭");
    }
}