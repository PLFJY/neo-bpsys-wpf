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
        /// <returns></returns>
        public string? PickImage();
        /// <summary>
        /// 选择Json文件
        /// </summary>
        /// <returns></returns>
        public string? PickJsonFile();
    }
}
