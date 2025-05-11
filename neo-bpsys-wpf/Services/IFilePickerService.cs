namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 提供文件选择服务的接口，包含图像和JSON文件的选择功能
    /// </summary>
    public interface IFilePickerService
    {
        /// <summary>
        /// 打开文件选择对话框以选取图像文件
        /// </summary>
        /// <returns>用户选择的图像文件路径，若取消操作则返回null</returns>
        public string? PickImage();

        /// <summary>
        /// 打开文件选择对话框以选取JSON配置文件
        /// </summary>
        /// <returns>用户选择的JSON文件路径，若取消操作则返回null</returns>
        public string? PickJsonFile();
    }
}