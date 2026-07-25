using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;

/// <summary>
/// Part Visual 发现的诊断严重级别。
/// </summary>
public enum FrontedV3PartVisualDiagnosticSeverity
{
    /// <summary>
    /// 警告级别，不阻止运行。
    /// </summary>
    Warning,

    /// <summary>
    /// 错误级别，表示 Part Visual 存在严重问题。
    /// </summary>
    Error
}

/// <summary>
/// Part Visual 发现的单条诊断信息。
/// </summary>
public sealed class FrontedV3PartVisualDiagnostic
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3PartVisualDiagnostic"/>。
    /// </summary>
    /// <param name="partId">关联的 Part 标识。</param>
    /// <param name="message">诊断消息。</param>
    /// <param name="severity">诊断严重级别。</param>
    public FrontedV3PartVisualDiagnostic(string partId, string message, FrontedV3PartVisualDiagnosticSeverity severity)
    {
        PartId = partId ?? throw new ArgumentNullException(nameof(partId));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Severity = severity;
    }

    /// <summary>
    /// 获取关联的 Part 标识。
    /// </summary>
    public string PartId { get; }

    /// <summary>
    /// 获取诊断消息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 获取诊断严重级别。
    /// </summary>
    public FrontedV3PartVisualDiagnosticSeverity Severity { get; }
}

/// <summary>
/// Part Visual 发现的结果，包含已发现的 Visual 映射与诊断列表。
/// </summary>
public sealed class FrontedV3PartVisualDiscoveryResult
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3PartVisualDiscoveryResult"/>。
    /// </summary>
    /// <param name="discoveredVisuals">已发现的 PartId → Visual 映射。</param>
    /// <param name="diagnostics">诊断列表。</param>
    public FrontedV3PartVisualDiscoveryResult(
        IReadOnlyDictionary<string, FrameworkElement> discoveredVisuals,
        IReadOnlyList<FrontedV3PartVisualDiagnostic> diagnostics)
    {
        DiscoveredVisuals = discoveredVisuals ?? throw new ArgumentNullException(nameof(discoveredVisuals));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    /// <summary>
    /// 获取已发现的 PartId → Visual 映射。
    /// </summary>
    public IReadOnlyDictionary<string, FrameworkElement> DiscoveredVisuals { get; }

    /// <summary>
    /// 获取诊断列表；为空时表示无问题。
    /// </summary>
    public IReadOnlyList<FrontedV3PartVisualDiagnostic> Diagnostics { get; }

    /// <summary>
    /// 获取是否存在诊断问题。
    /// </summary>
    public bool HasDiagnostics => Diagnostics.Count > 0;
}

/// <summary>
/// Part Visual 发现器，同时支持 XAML 附加属性与 C# 特性两种声明方式，并输出诊断。
/// </summary>
/// <remarks>
/// <para>
/// 该发现器扫描控件的视觉树与公共属性：
/// <list type="bullet">
/// <item>XAML：<c>fronted:FrontedV3.PartId="Logo"</c> 附加属性。</item>
/// <item>C#：<c>[FrontedV3PartVisual("Logo")]</c> 特性标注的属性。</item>
/// </list>
/// 两种声明方式解析后映射到同一个 Part。
/// </para>
/// <para>
/// 缺失或重复 Visual 输出诊断（warning），不崩溃 Designer，不破坏 Config：
/// <list type="bullet">
/// <item>声明了 Part 但未找到对应 Visual → warning。</item>
/// <item>多个 Visual 映射到同一 PartId → warning，使用第一个发现的 Visual。</item>
/// </list>
/// </para>
/// <para>
/// 该发现器位于 Core 项目，通过反射读取 PluginSdk 中的
/// <c>FrontedV3PartVisualAttribute</c>，避免 Core → PluginSdk 的循环引用。
/// </para>
/// </remarks>
public static class FrontedV3PartVisualResolver
{
    private const string PartVisualAttributeTypeName = "FrontedV3PartVisualAttribute";
    private const string PartIdPropertyName = "PartId";

    /// <summary>
    /// 从指定控件发现 Part Visual，同时扫描 XAML 附加属性与 C# 特性。
    /// </summary>
    /// <param name="control">要扫描的控件实例。</param>
    /// <param name="partDefinitions">该控件声明的 Part 定义列表。</param>
    /// <param name="logger">可选日志，用于记录诊断。</param>
    /// <returns>包含已发现 Visual 与诊断的结果。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="control"/> 或 <paramref name="partDefinitions"/> 为 <see langword="null"/> 时抛出。</exception>
    public static FrontedV3PartVisualDiscoveryResult Resolve(
        FrameworkElement control,
        IReadOnlyList<FrontedV3PartDefinition> partDefinitions,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(partDefinitions);

        var visualByPartId = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        var diagnostics = new List<FrontedV3PartVisualDiagnostic>();

        // 1. 扫描 C# 特性标注的属性
        DiscoverFromAttributes(control, visualByPartId, diagnostics, logger);

        // 2. 扫描视觉树中的 XAML 附加属性
        DiscoverFromVisualTree(control, visualByPartId, diagnostics, logger);

        // 3. 检查缺失 Visual
        CheckMissingVisuals(partDefinitions, visualByPartId, diagnostics, logger);

        return new FrontedV3PartVisualDiscoveryResult(visualByPartId, diagnostics);
    }

    private static void DiscoverFromAttributes(
        FrameworkElement control,
        Dictionary<string, FrameworkElement> visualByPartId,
        List<FrontedV3PartVisualDiagnostic> diagnostics,
        ILogger? logger)
    {
        var controlType = control.GetType();
        var properties = controlType.GetProperties(BindingFlags.Instance | BindingFlags.Public);

        foreach (var property in properties)
        {
            var attribute = FindPartVisualAttribute(property);
            if (attribute is null)
            {
                continue;
            }

            var partId = GetPartIdFromAttribute(attribute);
            if (string.IsNullOrEmpty(partId))
            {
                continue;
            }

            if (!typeof(FrameworkElement).IsAssignableFrom(property.PropertyType))
            {
                diagnostics.Add(new FrontedV3PartVisualDiagnostic(
                    partId!,
                    $"Property '{property.Name}' annotated with [FrontedV3PartVisual] does not return a FrameworkElement.",
                    FrontedV3PartVisualDiagnosticSeverity.Warning));
                logger?.LogWarning(
                    "Part visual attribute on property {PropertyName} of {ControlType} does not return a FrameworkElement.",
                    property.Name,
                    controlType.FullName);
                continue;
            }

            try
            {
                if (property.GetValue(control) is FrameworkElement visual)
                {
                    if (visualByPartId.ContainsKey(partId!))
                    {
                        diagnostics.Add(new FrontedV3PartVisualDiagnostic(
                            partId!,
                            $"Duplicate part visual for PartId '{partId}' found on property '{property.Name}'.",
                            FrontedV3PartVisualDiagnosticSeverity.Warning));
                        logger?.LogWarning(
                            "Duplicate part visual for PartId {PartId} on property {PropertyName} of {ControlType}.",
                            partId,
                            property.Name,
                            controlType.FullName);
                        continue;
                    }

                    visualByPartId[partId!] = visual;
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new FrontedV3PartVisualDiagnostic(
                    partId!,
                    $"Failed to read part visual from property '{property.Name}': {ex.Message}",
                    FrontedV3PartVisualDiagnosticSeverity.Warning));
                logger?.LogWarning(
                    ex,
                    "Failed to read part visual from property {PropertyName} of {ControlType}.",
                    property.Name,
                    controlType.FullName);
            }
        }
    }

    private static void DiscoverFromVisualTree(
        FrameworkElement control,
        Dictionary<string, FrameworkElement> visualByPartId,
        List<FrontedV3PartVisualDiagnostic> diagnostics,
        ILogger? logger)
    {
        foreach (var descendant in EnumerateDescendants(control))
        {
            var partId = FrontedV3.GetPartId(descendant);
            if (string.IsNullOrEmpty(partId))
            {
                continue;
            }

            if (visualByPartId.ContainsKey(partId!))
            {
                diagnostics.Add(new FrontedV3PartVisualDiagnostic(
                    partId!,
                    $"Duplicate part visual for PartId '{partId}' found in visual tree.",
                    FrontedV3PartVisualDiagnosticSeverity.Warning));
                logger?.LogWarning(
                    "Duplicate part visual for PartId {PartId} in visual tree of {ControlType}.",
                    partId,
                    control.GetType().FullName);
                continue;
            }

            if (descendant is not FrameworkElement frameworkElement)
            {
                continue;
            }

            visualByPartId[partId!] = frameworkElement;
        }
    }

    private static void CheckMissingVisuals(
        IReadOnlyList<FrontedV3PartDefinition> partDefinitions,
        Dictionary<string, FrameworkElement> visualByPartId,
        List<FrontedV3PartVisualDiagnostic> diagnostics,
        ILogger? logger)
    {
        foreach (var part in partDefinitions)
        {
            if (!visualByPartId.ContainsKey(part.Id))
            {
                diagnostics.Add(new FrontedV3PartVisualDiagnostic(
                    part.Id,
                    $"Part '{part.Id}' is declared but no visual was found.",
                    FrontedV3PartVisualDiagnosticSeverity.Warning));
                logger?.LogWarning(
                    "Part {PartId} is declared but no visual was found.",
                    part.Id);
            }
        }
    }

    private static Attribute? FindPartVisualAttribute(PropertyInfo property)
    {
        foreach (var attr in property.GetCustomAttributes())
        {
            if (attr.GetType().Name == PartVisualAttributeTypeName)
            {
                return attr;
            }
        }

        return null;
    }

    private static string? GetPartIdFromAttribute(Attribute attribute)
    {
        var partIdProperty = attribute.GetType().GetProperty(
            PartIdPropertyName,
            BindingFlags.Public | BindingFlags.Instance);
        return partIdProperty?.GetValue(attribute) as string;
    }

    private static IEnumerable<DependencyObject> EnumerateDescendants(DependencyObject parent)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;

            foreach (var descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
