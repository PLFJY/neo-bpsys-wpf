using System.Reflection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;

/// <summary>
/// v3 前台控件固定 Part 的声明，作为控件类上的 <c>public static readonly</c> 字段使用。
/// </summary>
/// <remarks>
/// <para>
/// 典型用法：
/// </para>
/// <code>
/// public static readonly FrontedV3Part LogoPart =
///     FrontedV3Part.Register&lt;TeamCardControl&gt;("Logo")
///         .WithSize(
///             FrontedV3Storage.ClrProperty("LogoWidth"),
///             FrontedV3Storage.ClrProperty("LogoHeight"))
///         .WithCapabilities(FrontedV3PartCapabilities.Resize);
/// </code>
/// <para>
/// 框架在注册控件时通过反射发现这些字段，转换为 <see cref="FrontedV3PartDefinition"/>。
/// </para>
/// </remarks>
public sealed class FrontedV3Part
{
    private readonly FrontedV3PartDefinition _definition;

    private FrontedV3Part(FrontedV3PartDefinition definition)
    {
        _definition = definition;
    }

    /// <summary>
    /// 获取该 Part 声明的控件类型。
    /// </summary>
    public Type ControlType { get; private set; } = null!;

    /// <summary>
    /// 获取 Part 标识。
    /// </summary>
    public string Id => _definition.Id;

    /// <summary>
    /// 获取 Part 的操作能力。
    /// </summary>
    public FrontedV3PartCapabilities Capabilities => _definition.Capabilities;

    /// <summary>
    /// 获取宽度存储访问器。
    /// </summary>
    public IFrontedV3StorageAccessor? WidthStorage => _definition.WidthStorage;

    /// <summary>
    /// 获取高度存储访问器。
    /// </summary>
    public IFrontedV3StorageAccessor? HeightStorage => _definition.HeightStorage;

    /// <summary>
    /// 获取 X 坐标存储访问器。
    /// </summary>
    public IFrontedV3StorageAccessor? XStorage => _definition.XStorage;

    /// <summary>
    /// 获取 Y 坐标存储访问器。
    /// </summary>
    public IFrontedV3StorageAccessor? YStorage => _definition.YStorage;

    /// <summary>
    /// 开始为指定控件类型注册固定 Part。
    /// </summary>
    /// <typeparam name="TControl">控件类型。</typeparam>
    /// <param name="id">Part 标识，在同一控件内必须唯一。</param>
    /// <returns>用于链式配置的 <see cref="FrontedV3Part"/> 实例。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="id"/> 为 <see langword="null"/> 时抛出。</exception>
    public static FrontedV3Part Register<TControl>(string id) where TControl : class
    {
        ArgumentNullException.ThrowIfNull(id);
        return new FrontedV3Part(new FrontedV3PartDefinition
        {
            Id = id,
            Capabilities = FrontedV3PartCapabilities.MoveAndResize
        })
        {
            ControlType = typeof(TControl)
        };
    }

    /// <summary>
    /// 设置 Part 的尺寸存储访问器。
    /// </summary>
    /// <param name="widthStorage">宽度存储访问器。</param>
    /// <param name="heightStorage">高度存储访问器。</param>
    /// <returns>当前 <see cref="FrontedV3Part"/> 实例，支持链式配置。</returns>
    public FrontedV3Part WithSize(
        IFrontedV3StorageAccessor? widthStorage,
        IFrontedV3StorageAccessor? heightStorage)
    {
        _definition.WidthStorage = widthStorage;
        _definition.HeightStorage = heightStorage;
        return this;
    }

    /// <summary>
    /// 设置 Part 的位置存储访问器。
    /// </summary>
    /// <param name="xStorage">X 坐标存储访问器。</param>
    /// <param name="yStorage">Y 坐标存储访问器。</param>
    /// <returns>当前 <see cref="FrontedV3Part"/> 实例，支持链式配置。</returns>
    public FrontedV3Part WithPosition(
        IFrontedV3StorageAccessor? xStorage,
        IFrontedV3StorageAccessor? yStorage)
    {
        _definition.XStorage = xStorage;
        _definition.YStorage = yStorage;
        return this;
    }

    /// <summary>
    /// 设置 Part 的操作能力。
    /// </summary>
    /// <param name="capabilities">操作能力。</param>
    /// <returns>当前 <see cref="FrontedV3Part"/> 实例，支持链式配置。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="capabilities"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3Part WithCapabilities(FrontedV3PartCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        _definition.Capabilities = capabilities;
        return this;
    }

    /// <summary>
    /// 将该 Part 声明转换为 <see cref="FrontedV3PartDefinition"/>。
    /// </summary>
    /// <returns>与该声明等价的 <see cref="FrontedV3PartDefinition"/>。</returns>
    public FrontedV3PartDefinition ToDefinition() => new(
        _definition.Id,
        _definition.Capabilities,
        _definition.WidthStorage,
        _definition.HeightStorage,
        _definition.XStorage,
        _definition.YStorage);

    /// <summary>
    /// 从控件类型上发现所有 <c>public static readonly FrontedV3Part</c> 字段并转换为定义列表。
    /// </summary>
    /// <param name="controlType">控件类型。</param>
    /// <returns>该控件声明的所有 Part 定义列表。</returns>
    public static IReadOnlyList<FrontedV3PartDefinition> Discover(Type controlType)
    {
        ArgumentNullException.ThrowIfNull(controlType);
        var fields = controlType.GetFields(BindingFlags.Public | BindingFlags.Static);
        var definitions = new List<FrontedV3PartDefinition>();

        foreach (var field in fields)
        {
            if (field.FieldType != typeof(FrontedV3Part))
            {
                continue;
            }

            if (field.GetValue(null) is not FrontedV3Part part)
            {
                continue;
            }

            definitions.Add(part.ToDefinition());
        }

        return definitions;
    }
}
