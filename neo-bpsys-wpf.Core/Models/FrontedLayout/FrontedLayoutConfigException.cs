namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 前台布局配置错误。
/// </summary>
public class FrontedLayoutConfigException : Exception
{
    /// <summary>
    /// 初始化布局配置异常。
    /// </summary>
    public FrontedLayoutConfigException(string message) : base(message)
    {
    }

    /// <summary>
    /// 初始化布局配置异常。
    /// </summary>
    public FrontedLayoutConfigException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
