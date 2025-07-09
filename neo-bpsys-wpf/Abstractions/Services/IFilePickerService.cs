namespace neo_bpsys_wpf.Abstractions.Services
{
    /// <summary>
    /// 文件选择服务接口
    /// </summary>
    public interface IFilePickerService
    {
        /// <summary>
        /// 选择图片
        /// </summary>
        /// <returns>文件路径</returns>
        public string? PickImage();
        /// <summary>
        /// 选择Json文件
        /// </summary>
        /// <returns>文件路径</returns>
        public string? PickJsonFile();
    }
}
