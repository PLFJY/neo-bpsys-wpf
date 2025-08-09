namespace neo_bpsys_wpf.Core.Extensions;

public interface IExtension
{
    /// <summary>
    /// 插件信息
    /// </summary>
    public ExtensionManifest ExtensionManifest { get; }

    /// <summary>
    /// 当插件被注册时调用，用于初始化插件的相关资源或状态。
    /// 可以在此方法中进行插件的必要设置，例如设置插件信息、注册插件UI等。
    /// </summary>
    public void Initialize()
    {
    }
    
    /// <summary>
    /// 当插件被注销时调用，用于释放插件的相关资源或状态。
    /// 可以在此方法中进行插件的必要清理，例如取消注册插件UI等
    /// </summary>
    public void Uninitialize()
    {
    }

    /// <summary>
    /// 当插件启用时调用
    /// </summary>
    public void OnEnable()
    {
    }


    /// <summary>
    /// 当插件禁用时调用
    /// </summary>
    public void OnDisable()
    {
    }
}