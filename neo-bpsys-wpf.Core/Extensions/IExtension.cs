namespace neo_bpsys_wpf.Core.Extensions;

public interface IExtension
{
    /// <summary>
    /// 插件信息
    /// </summary>
    public ExtensionInfo ExtensionInfo { get; internal set; }
    /// <summary>
    /// 当插件被注册时调用，用于初始化插件的相关资源或状态。
    /// </summary>
    public void Initialize();
    /// <summary>
    /// 当插件启用时调用
    /// </summary>
    public void OnEnable();
    /// <summary>
    /// 当插件禁用时调用
    /// </summary>
    public void OnDisable();
}