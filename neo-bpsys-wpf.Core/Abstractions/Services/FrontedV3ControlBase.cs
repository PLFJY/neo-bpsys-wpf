using System.ComponentModel;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Options;

namespace neo_bpsys_wpf.PluginSdk;

/// <summary>
/// v3 前台控件的抽象基类，所有新注册的 v3 控件必须继承自此类型。
/// </summary>
/// <remarks>
/// <para>
/// 该基类继承自 <see cref="UserControl"/>，因此支持 XAML 声明式视觉树，也支持在代码中构建视觉树。
/// 控件在创建后由宿主通过 <see cref="InitializeFrontedV3"/> 注入运行时上下文，
/// 控件通过 <see cref="Context"/> 访问服务、共享数据、资源解析器与当前配置。
/// </para>
/// <para>
/// 控件 <b>不</b>管理自身的 Canvas 坐标（<c>Left</c>/<c>Top</c>/<c>Width</c>/<c>Height</c>/
/// <c>ZIndex</c>/<c>Visibility</c>/<c>GaussianBlur</c>），这些由 Phase 2 引入的
/// <c>FrontedV3ControlHost</c> 统一负责。控件只负责矩形区域内的视觉内容。
/// </para>
/// <para>
/// 属性编辑通过 <see cref="Options"/> 动态代理视图完成：XAML 绑定
/// <c>{Binding Appearance.TextColor}</c> 最终委托到对应
/// <see cref="neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties.FrontedV3PropertyDefinition"/> 的 Storage 访问器，
/// 读写直接作用于当前 <see cref="Core.Models.FrontedLayout.FrontedControlConfigBase"/>，不缓存独立值。
/// </para>
/// <para>
/// 该类型定义在 Core 程序集中（命名空间 <c>neo_bpsys_wpf.PluginSdk</c> 以保持插件 API 兼容），
/// 使内置控件（也在 Core 中）能够直接继承，避免 Core → PluginSdk 的循环引用。
/// </para>
/// </remarks>
public abstract class FrontedV3ControlBase : UserControl
{
    private FrontedV3ControlContext? _context;

    /// <summary>
    /// 获取控件运行时上下文；未调用 <see cref="InitializeFrontedV3"/> 前为 <see langword="null"/>。
    /// </summary>
    public FrontedV3ControlContext? Context => _context;

    /// <summary>
    /// 获取由属性 Schema 构建的 Options 动态代理视图；未初始化时为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// XAML 中可将 <c>DataContext</c> 设置为 <see cref="Options"/>，使绑定路径
    /// <c>{Binding Appearance.TextColor}</c> 通过 <see cref="ICustomTypeDescriptor"/> 发现动态属性。
    /// </remarks>
    public FrontedV3OptionsView? Options => _context?.Options;

    /// <summary>
    /// 由宿主调用，注入运行时上下文并触发 <see cref="OnInitializeFrontedV3"/>。
    /// </summary>
    /// <param name="context">控件运行时上下文。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="context"/> 为 <see langword="null"/> 时抛出。</exception>
    public void InitializeFrontedV3(FrontedV3ControlContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        OnInitializeFrontedV3(context);
    }

    /// <summary>
    /// 派生类重写此方法以在上下文注入后执行自定义初始化，例如设置 <c>DataContext</c>、
    /// 建立绑定或解析资源。
    /// </summary>
    /// <param name="context">控件运行时上下文。</param>
    protected virtual void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
    }
}
