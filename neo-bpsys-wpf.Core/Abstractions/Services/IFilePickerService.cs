namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 文件选择服务接口
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// 选择 bpui 文件
    /// </summary>
    /// <returns></returns>
    string? PickBpuiFile();

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

    /// <summary>
    /// 选择Zip文件
    /// </summary>
    /// <returns>文件路径</returns>
    public string? PickZipFile();

    /// <summary>
    /// 选择 JSON 导出保存路径。
    /// </summary>
    /// <param name="defaultFileName">默认文件名。</param>
    /// <returns>文件路径。</returns>
    public string? SaveJsonFile(string defaultFileName);

    /// <summary>
    /// 选择 BPUI 导出保存路径。
    /// </summary>
    /// <param name="defaultFileName">默认文件名。</param>
    /// <returns>文件路径。</returns>
    public string? SaveBpuiFile(string defaultFileName);
}
