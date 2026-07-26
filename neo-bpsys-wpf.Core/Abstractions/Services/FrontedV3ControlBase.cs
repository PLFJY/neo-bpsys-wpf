using System.ComponentModel;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Options;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

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
/// <c>ZIndex</c>/<c>Visibility</c>/<c>GaussianBlur</c>），这些由
/// <c>FrontedV3ControlHost</c> 统一负责。控件只负责矩形区域内的视觉内容。
/// </para>
/// <para>
/// <b>DataContext 命名空间约定</b>：基类在 <see cref="InitializeFrontedV3"/> 中将
/// <see cref="FrameworkElement.DataContext"/> 统一设置为完整的 <see cref="FrontedV3ControlContext"/>，
/// 派生控件无需自行设置 DataContext。XAML 与代码绑定应通过 <c>Options.*</c> 根命名空间访问 V3 属性，
/// 例如 <c>{Binding Options.Appearance.TextColor}</c>，以明确隔离 WPF 自带属性与 V3 注册属性，
/// 避免误绑定到 FrameworkElement 的内建属性。
/// </para>
/// <para>
/// 属性编辑通过 <see cref="Options"/> 动态代理视图完成：XAML 绑定
/// <c>{Binding Options.Appearance.TextColor}</c> 最终委托到对应
/// <see cref="neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties.FrontedV3PropertyDefinition"/> 的 Storage 访问器，
/// 读写直接作用于当前 <see cref="Core.Models.FrontedLayout.FrontedControlConfigBase"/>，不缓存独立值。
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
    /// <para>
    /// 基类已在 <see cref="InitializeFrontedV3"/> 中将 <see cref="FrameworkElement.DataContext"/>
    /// 设置为完整的 <see cref="FrontedV3ControlContext"/>，XAML 与代码绑定应通过
    /// <c>Options.*</c> 根命名空间访问该视图，例如 <c>{Binding Options.Appearance.TextColor}</c>。
    /// </para>
    /// <para>
    /// 派生控件通常无需直接读取该属性；仅在需要把 Options 视图作为子元素 DataContext 时使用。
    /// </para>
    /// </remarks>
    public FrontedV3OptionsView? Options => _context?.Options;

    /// <summary>
    /// 由宿主调用，注入运行时上下文并触发 <see cref="OnInitializeFrontedV3"/>。
    /// </summary>
    /// <param name="context">控件运行时上下文。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="context"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <remarks>
    /// 基类在该方法中将 <see cref="FrameworkElement.DataContext"/> 统一设置为 <paramref name="context"/>，
    /// 使 XAML 绑定可通过 <c>Options.*</c> 根命名空间访问 V3 属性。派生控件无需自行设置 DataContext。
    /// </remarks>
    public void InitializeFrontedV3(FrontedV3ControlContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        // 统一把 DataContext 设置为完整 Context（而非仅 Options），让 XAML 绑定强制走 Options.* 根命名空间，
        // 明确隔离 WPF 自带属性与 V3 注册属性，避免误绑定到 FrameworkElement 内建属性。
        DataContext = context;
        OnInitializeFrontedV3(context);
    }

    /// <summary>
    /// 派生类重写此方法以在上下文注入后执行自定义初始化，例如建立绑定或解析资源。
    /// </summary>
    /// <param name="context">控件运行时上下文。</param>
    /// <remarks>
    /// 派生控件 <b>无需</b> 在重写中设置 <c>DataContext</c>，基类已统一完成；只需关注自身的视觉树与绑定构建。
    /// </remarks>
    protected virtual void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
    }
}
