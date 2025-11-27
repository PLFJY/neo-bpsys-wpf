namespace neo_bpsys.Core.Abstractions.Services;

public interface IMessageBoxService
{
    Task<bool> ShowDeleteConfirmAsync(string title, string message, string primaryButtonText = "确认", string secondaryButtonText = "取消");
    Task ShowErrorAsync(string message, string title = "错误", string closeButtonText = "关闭");
    Task ShowInfoAsync(string message, string title = "提示", string closeButtonText = "关闭");
    Task<bool> ShowConfirmAsync(string title, string message, string primaryButtonText = "确认", string secondaryButtonText = "取消");
}
