using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Services
{
    public class MessageBoxService : IMessageBoxService
    {
        /// <summary>
        /// 显示删除确认对话框
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="primaryButtonText"></param>
        /// <param name="secondaryButtonText"></param>
        /// <returns></returns>
        public async Task<bool> ShowDeleteConfirmAsync(string title, string message, string primaryButtonText = "确认", string secondaryButtonText = "取消")
        {
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
        /// 显示信息对话框
        /// </summary>
        /// <param name="message"></param>
        /// <param name="title"></param>
        /// <param name="closeButtonText"></param>
        /// <returns></returns>
        public async Task ShowInfoAsync(string message, string title = "提示", string closeButtonText = "关闭")
        {
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
        /// 显示错误对话框
        /// </summary>
        /// <param name="message"></param>
        /// <param name="title"></param>
        /// <param name="closeButtonText"></param>
        /// <returns></returns>
        public async Task ShowErrorAsync(string message, string title = "错误", string closeButtonText = "关闭")
        {
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
        /// 显示确认对话框
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="primaryButtonText"></param>
        /// <param name="secondaryButtonText"></param>
        /// <returns></returns>
        public async Task<bool> ShowConfirmAsync(string title, string message, string primaryButtonText = "确认", string secondaryButtonText = "取消")
        {
            var messageBox = new MessageBox()
            {
                Title = title,
                Content = message,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = secondaryButtonText,
                CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Dismiss24 },
                PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Checkmark24 }
            };
            var result = await messageBox.ShowDialogAsync();

            return result == MessageBoxResult.Primary;
        }
    }
}
