using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions;

/// <summary>
/// 所有扩展应当包含一个实现 IExtension 的类，以作为扩展的入口。
/// 该类必须使用 <see cref="ExtensionManifest"/> 特性进行装饰，以声明插件的必要信息。
/// </summary>
public interface IExtension
{
    public IExtensionService ExtensionService { get; set; }
    
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