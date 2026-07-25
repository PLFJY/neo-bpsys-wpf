namespace neo_bpsys_wpf.PluginSdk;

/// <summary>
/// 标注一个 v3 前台控件类型，并携带控件局部标识（ControlId）与 Designer 展示元数据。
/// </summary>
/// <remarks>
/// <para>
/// 该特性由 <c>FrontedV3ControlRegistryExtensions.AddFrontedV3Control&lt;TControl&gt;</c>
/// 在注册时读取，用于生成控件的 Canonical Control Type：
/// <list type="bullet">
/// <item>内置控件（<see cref="IsBuiltIn"/> 为 <see langword="true"/>）：直接使用 <see cref="ControlId"/>，例如 <c>Text</c>。</item>
/// <item>插件控件：使用 <c>plugin:{PackageId}/{ControlId}</c>，例如 <c>plugin:plfjy.ExamplePlugin/TeamCard</c>。</item>
/// </list>
/// </para>
/// <para>
/// <see cref="ControlId"/> 只接受安全的局部标识：非空、非纯空白，且不含 <c>/</c>、<c>\</c>、<c>:</c>，
/// 也不允许直接传入完整的 canonical ID（<c>plugin:package/control</c> 形式）。
/// 不同插件可以复用相同的 <see cref="ControlId"/>。
/// </para>
/// <para>
/// <see cref="IsBuiltIn"/> 仅供宿主注册代码使用。插件在插件初始化作用域内设置
/// <see cref="IsBuiltIn"/> 为 <see langword="true"/> 会被拒绝，插件控件无法通过该标记逃离自己的命名空间。
/// </para>
/// <para>
/// Designer 展示元数据（<see cref="DisplayNameKey"/>、<see cref="DescriptionKey"/>、<see cref="Icon"/>、
/// <see cref="DefaultWidth"/>、<see cref="DefaultHeight"/>、<see cref="DisplayOrder"/>）
/// 由 Attribute 直接声明，注册时通过 <c>FrontedV3ControlRegistryExtensions.BuildMetadata</c>
/// 推导为 <c>FrontedV3ControlMetadata</c> 暴露到 Registration。
/// 所有元数据字段可选，缺失时由调用方按合理默认值回退。
/// </para>
/// <para>
/// 该类型定义在 Core 程序集中（命名空间 <c>neo_bpsys_wpf.PluginSdk</c> 以保持插件 API 兼容）。
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class FrontedV3ControlAttribute : Attribute
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3ControlAttribute"/>。
    /// </summary>
    /// <param name="controlId">控件局部标识，必须通过 <c>FrontedV3ControlIdValidator</c> 验证。</param>
    public FrontedV3ControlAttribute(string controlId)
    {
        ControlId = controlId;
    }

    /// <summary>
    /// 控件局部标识，例如 <c>TeamCard</c> 或内置的 <c>Text</c>。
    /// </summary>
    public string ControlId { get; }

    /// <summary>
    /// 是否为宿主内置控件。仅供宿主注册代码设置为 <see langword="true"/>；
    /// 插件在插件作用域内设置该值为 <see langword="true"/> 会被拒绝。
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// 是否在 Designer 中显示"应用到同类型控件"按钮。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 默认为 <see langword="false"/>。仅声明 <see cref="SupportsPeerStyleTransfer"/> 为
    /// <see langword="true"/> 的控件在 Designer 属性面板中可见"应用到同类型控件"按钮，
    /// 并可参与同类型 peer 之间的外观样式传播。
    /// </para>
    /// <para>
    /// 业务上目前仅 <c>MapV2Display</c> 内置控件需要此入口；其他内置控件与插件控件
    /// 不应设置该属性，按钮不出现。
    /// </para>
    /// </remarks>
    public bool SupportsPeerStyleTransfer { get; set; }

    /// <summary>
    /// 控件在 Designer 控件目录中显示的本地化键；为 <see langword="null"/> 时回退到 <see cref="ControlId"/>。
    /// </summary>
    public string? DisplayNameKey { get; set; }

    /// <summary>
    /// 控件描述的本地化键；为 <see langword="null"/> 时 Designer 不显示描述。
    /// </summary>
    public string? DescriptionKey { get; set; }

    /// <summary>
    /// 控件在 Designer 控件目录中显示的图标资源名（WPF-UI Symbol 或资源键）；
    /// 为 <see langword="null"/> 时显示默认图标。
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 新添加控件时的默认根宽度；为 <see cref="double.NaN"/> 时由 Designer 按最小命中框回退。
    /// </summary>
    /// <remarks>
    /// 由于特性属性类型限制，使用 <see cref="double.NaN"/> 作为"未设置"哨兵值，而非 <c>null</c>。
    /// </remarks>
    public double DefaultWidth { get; set; } = double.NaN;

    /// <summary>
    /// 新添加控件时的默认根高度；为 <see cref="double.NaN"/> 时由 Designer 按最小命中框回退。
    /// </summary>
    /// <remarks>
    /// 由于特性属性类型限制，使用 <see cref="double.NaN"/> 作为"未设置"哨兵值，而非 <c>null</c>。
    /// </remarks>
    public double DefaultHeight { get; set; } = double.NaN;

    /// <summary>
    /// 控件在 Designer 控件目录中的显示顺序；为 <see langword="null"/> 时按注册顺序追加。
    /// </summary>
    public int? DisplayOrder { get; set; }
}
