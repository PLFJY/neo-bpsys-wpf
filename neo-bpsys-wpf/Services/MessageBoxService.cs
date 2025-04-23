using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Services
{
    public class MessageBoxService : IMessageBoxService
    {
        public async Task<bool> ShowDeleteConfirmAsync(
            string title,
            string message,
            string primaryButtonText = "确认",
            string secondaryButtonText = "取消"
        )
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

        public async Task<bool> ShowExitConfirmAsync(
            string title,
            string message,
            string primaryButtonText = "退出",
            string secondaryButtonText = "取消"
        )
        {
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
    }
}
