using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

/// <summary>
/// 按模板重新分配集合项布局时由 Designer 提供给控件回调的上下文。
/// </summary>
/// <remarks>
/// <para>
/// 该上下文为 <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 回调提供 Designer 侧信息，
/// 让控件可以根据当前编辑状态（例如 BO3/BO5）或被点击的具名模板决定如何重新分配子项位置与可见性。
/// </para>
/// <para>
/// 上下文携带的字段：
/// <list type="bullet">
/// <item><see cref="Services"/>：DI 服务提供器，控件可解析 <see cref="ISharedDataService"/> 等运行时服务。</item>
/// <item><see cref="CurrentBoModeState"/>：当前正在编辑的 BO 状态（BO3/BO5）。控件据此选择对应模板。</item>
/// <item><see cref="WindowTypeName"/>：当前编辑的窗口类型名（如 <c>BpWindow</c>）。</item>
/// <item><see cref="CanvasName"/>：当前编辑的 Canvas 名称。</item>
/// <item><see cref="Document"/>：当前 Designer 文档实例，控件可读取 Canvas 配置、其他控件等信息。</item>
/// <item><see cref="TemplateId"/>：当 <see cref="FrontedV3PartCollectionDefinition.Templates"/> 非空且用户点击了具名模板按钮时，
/// 该字段为被点击模板的 Id；否则为 <see langword="null"/>，控件应回退到基于 <see cref="CurrentBoModeState"/> 的默认模板。</item>
/// </list>
/// </para>
/// <para>
/// 回调只负责位置/可见性的模板分配，不修改外观属性（Color/FontFamily 等）。
/// </para>
/// </remarks>
public sealed class FrontedV3TemplateContext
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3TemplateContext"/>。
    /// </summary>
    public FrontedV3TemplateContext()
    {
    }

    /// <summary>
    /// 初始化 <see cref="FrontedV3TemplateContext"/> 并指定全部属性。
    /// </summary>
    /// <param name="services">DI 服务提供器。</param>
    /// <param name="currentBoModeState">当前编辑的 BO 状态。</param>
    /// <param name="windowTypeName">当前编辑的窗口类型名。</param>
    /// <param name="canvasName">当前编辑的 Canvas 名称。</param>
    /// <param name="document">当前 Designer 文档实例。</param>
    /// <param name="templateId">被点击的具名模板 Id；无具名模板时为 <see langword="null"/>。</param>
    public FrontedV3TemplateContext(
        IServiceProvider services,
        FrontedCanvasBoModeState currentBoModeState,
        string windowTypeName,
        string canvasName,
        FrontedCanvasDesignDocument? document,
        string? templateId)
    {
        Services = services;
        CurrentBoModeState = currentBoModeState;
        WindowTypeName = windowTypeName;
        CanvasName = canvasName;
        Document = document;
        TemplateId = templateId;
    }

    /// <summary>
    /// 获取或设置 DI 服务提供器，控件可解析运行时服务。
    /// </summary>
    public IServiceProvider Services { get; set; } = EmptyServiceProvider.Instance;

    /// <summary>
    /// 获取或设置当前编辑的 BO 状态（BO3/BO5）。
    /// </summary>
    public FrontedCanvasBoModeState CurrentBoModeState { get; set; } = FrontedCanvasBoModeState.Bo5;

    /// <summary>
    /// 获取或设置当前编辑的窗口类型名（如 <c>BpWindow</c>）。
    /// </summary>
    public string WindowTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置当前编辑的 Canvas 名称。
    /// </summary>
    public string CanvasName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置当前 Designer 文档实例，控件可读取 Canvas 配置、其他控件等信息；测试场景下可为 <see langword="null"/>。
    /// </summary>
    public FrontedCanvasDesignDocument? Document { get; set; }

    /// <summary>
    /// 获取或设置被点击的具名模板 Id；无具名模板（控件未声明 <see cref="FrontedV3PartCollectionDefinition.Templates"/>）
    /// 或调用方未指定时为 <see langword="null"/>，控件应回退到基于 <see cref="CurrentBoModeState"/> 的默认模板。
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    /// 创建一个使用空服务提供器与 BO5 默认状态的默认上下文，供测试与无 Designer 链路场景使用。
    /// </summary>
    /// <returns>默认 <see cref="FrontedV3TemplateContext"/> 实例。</returns>
    public static FrontedV3TemplateContext Default() => new();

    /// <summary>
    /// 不解析任何服务的空 <see cref="IServiceProvider"/>，作为 <see cref="Services"/> 的默认值。
    /// </summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
