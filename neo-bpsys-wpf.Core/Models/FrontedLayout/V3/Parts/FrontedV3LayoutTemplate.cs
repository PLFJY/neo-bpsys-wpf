namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

/// <summary>
/// 描述一个具名布局模板（如 BO3、BO5、Default、Compact），
/// 由控件在 <see cref="FrontedV3PartCollectionDefinition.Templates"/> 中声明，
/// 供 Designer 在属性面板渲染为独立的"按模板重新分配"按钮。
/// </summary>
/// <remarks>
/// <para>
/// 当 <see cref="FrontedV3PartCollectionDefinition.Templates"/> 非空时，Designer 将不再渲染单一通用按钮，
/// 而是为每个模板渲染一个按钮，点击后调用 <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/>
/// 并通过 <see cref="FrontedV3TemplateContext.TemplateId"/> 传递被点击模板的 <see cref="Id"/>。
/// </para>
/// <para>
/// 当 <see cref="FrontedV3PartCollectionDefinition.Templates"/> 为空但
/// <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 非 <see langword="null"/> 时，
/// Designer 渲染单一通用按钮，点击后调用回调且 <see cref="FrontedV3TemplateContext.TemplateId"/> 为
/// <see langword="null"/>，控件应回退到基于 <see cref="FrontedV3TemplateContext.CurrentBoModeState"/> 的默认模板。
/// </para>
/// </remarks>
public sealed class FrontedV3LayoutTemplate
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3LayoutTemplate"/>。
    /// </summary>
    public FrontedV3LayoutTemplate()
    {
    }

    /// <summary>
    /// 初始化 <see cref="FrontedV3LayoutTemplate"/> 并指定 Id 与 DisplayNameKey。
    /// </summary>
    /// <param name="id">模板唯一标识，在同一 PartCollection 内必须唯一。</param>
    /// <param name="displayNameKey">模板显示名称的本地化资源 Key。</param>
    public FrontedV3LayoutTemplate(string id, string displayNameKey)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayNameKey = displayNameKey ?? throw new ArgumentNullException(nameof(displayNameKey));
    }

    /// <summary>
    /// 获取或设置模板唯一标识，在同一 PartCollection 内必须唯一（如 <c>BO3</c>、<c>BO5</c>、<c>Default</c>）。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置模板显示名称的本地化资源 Key（如 <c>Designer.PropertyEditor.LayoutTemplate.BO3</c>）。
    /// </summary>
    public string DisplayNameKey { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置模板描述的本地化资源 Key；为空字符串时不显示描述。
    /// </summary>
    public string? DescriptionKey { get; set; }

    /// <summary>
    /// 获取或设置模板按钮的图标符号名（WPF-UI SymbolIcon）；为 <see langword="null"/> 时使用默认图标。
    /// </summary>
    public string? Icon { get; set; }
}
