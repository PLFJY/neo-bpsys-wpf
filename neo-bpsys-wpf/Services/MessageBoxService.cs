using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 提供标准化的消息对话框服务，用于显示确认对话框和信息提示
    /// </summary>
    public class MessageBoxService : IMessageBoxService
    {
        /// <summary>
        /// 显示删除确认对话框（带主/次操作按钮）
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="message">显示的消息内容</param>
        /// <param name="primaryButtonText">主操作按钮文本（默认"确认"）</param>
        /// <param name="secondaryButtonText">取消操作按钮文本（默认"取消"）</param>
        /// <returns>当用户点击主操作按钮时返回true，否则返回false</returns>
        public async Task<bool> ShowDeleteConfirmAsync(
            string title,
            string message,
            string primaryButtonText = "确认",
            string secondaryButtonText = "取消"
        )
        {
            // 配置删除确认对话框的特殊图标和按钮
            var messageBox = new MessageBox()
            {
                Title = title,
                Content = message,
                PrimaryButtonText = primaryButtonText,
                PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Delete24 },
                CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
                CloseButtonText = secondaryButtonText,
            };
            var result = await messageBox.ShowDialogAsync();

            return result == MessageBoxResult.Primary;
        }

        /// <summary>
        /// 显示退出确认对话框（带主/次操作按钮）
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="message">显示的消息内容</param>
        /// <param name="primaryButtonText">主操作按钮文本（默认"退出"）</param>
        /// <param name="secondaryButtonText">取消操作按钮文本（默认"取消"）</param>
        /// <returns>当用户点击主操作按钮时返回true，否则返回false</returns>
        public async Task<bool> ShowExitConfirmAsync(
            string title,
            string message,
            string primaryButtonText = "退出",
            string secondaryButtonText = "取消"
        )
        {
            // 配置退出确认对话框的特殊图标和按钮
            var messageBox = new MessageBox()
            {
                Title = title,
                Content = message,
                PrimaryButtonText = primaryButtonText,
                PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowExit20 },
                CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
                CloseButtonText = secondaryButtonText,
            };
            var result = await messageBox.ShowDialogAsync();

            return result == MessageBoxResult.Primary;
        }

        /// <summary>
        /// 显示信息提示对话框（仅关闭按钮）
        /// </summary>
        /// <param name="message">显示的消息内容</param>
        /// <param name="title">对话框标题（默认"提示"）</param>
        /// <param name="closeButtonText">关闭按钮文本（默认"关闭"）</param>
        public async Task ShowInfoAsync(string message, string title = "提示", string closeButtonText = "关闭")
        {
            // 配置标准信息提示对话框
            var messageBox = new MessageBox()
            {
                Title = title,
                Content = message,
                CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Dismiss24 },
                CloseButtonText = closeButtonText,
            };

            await messageBox.ShowDialogAsync();
        }

        /// <summary>
        /// 显示警告提示对话框（仅关闭按钮）
        /// </summary>
        /// <param name="message">显示的消息内容</param>
        /// <param name="title">对话框标题（默认"警告"）</param>
        /// <param name="closeButtonText">关闭按钮文本（默认"关闭"）</param>
        public async Task ShowWarningAsync(string message, string title = "警告", string closeButtonText = "关闭")
        {
            // 配置标准警告提示对话框（结构与信息提示相同，通过标题区分类型）
            var messageBox = new MessageBox()
            {
                Title = title,
                Content = message,
                CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Dismiss24 },
                CloseButtonText = closeButtonText,
            };

            await messageBox.ShowDialogAsync();
        }
    }
}