namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>Web 控件适配器注册表；当前仅声明受控元数据，不加载第三方脚本。</summary>
public interface IWebControlRegistry
{
    /// <summary>按稳定的 <c>plugin:*</c> ControlType 查询适配器描述符。</summary>
    /// <param name="controlType">完整控件类型。</param>
    /// <param name="descriptor">已注册的描述符。</param>
    /// <returns>找到时为 <see langword="true"/>。</returns>
    bool TryGet(string controlType, out WebControlAdapterDescriptor descriptor);
}

/// <summary>未来第三方 Web 控件适配器的可序列化身份描述。</summary>
public sealed record WebControlAdapterDescriptor(string ControlType, string AdapterId);

/// <summary>默认空注册表，使未适配控件稳定回退到诊断占位。</summary>
public sealed class WebControlRegistry : IWebControlRegistry
{
    /// <inheritdoc />
    public bool TryGet(string controlType, out WebControlAdapterDescriptor descriptor)
    {
        descriptor = null!;
        return false;
    }
}
