namespace neo_bpsys_wpf.Core.Services.Registry;

/// <summary>
/// 在插件初始化期间携带当前插件包 ID 的宿主内部注册作用域。
/// </summary>
/// <remarks>
/// 该类型为宿主内部使用：宿主在调用 <c>PluginBase.Initialize</c> 前通过
/// <see cref="BeginScope"/> 建立作用域，使插件代码在注册前台窗口等资源时能够隐式关联到当前插件包。
/// 作用域通过 <see cref="AsyncLocal{T}"/> 携带，按异步执行流隔离，并在 <see cref="IDisposable.Dispose"/>
/// 时恢复上一层值（包括异常退出场景，配合 <c>using</c> 语句保证）。
/// </remarks>
internal static class FrontedPluginRegistrationContext
{
    private static readonly AsyncLocal<string?> _currentPackageId = new();

    /// <summary>
    /// 获取当前异步执行流中活跃的插件包 ID；不在任何插件作用域内时为 <see langword="null"/>。
    /// </summary>
    public static string? CurrentPackageId => _currentPackageId.Value;

    /// <summary>
    /// 开始一个新的插件注册作用域，将 <see cref="CurrentPackageId"/> 设置为 <paramref name="packageId"/>。
    /// </summary>
    /// <param name="packageId">当前插件的包 ID；非插件宿主直接注册时为 <see langword="null"/>。</param>
    /// <returns>一个 <see cref="IDisposable"/>，释放时恢复进入作用域前的上一层值。</returns>
    /// <remarks>
    /// 返回的作用域对象应在 <c>using</c> 语句中使用，以确保即使发生异常也能恢复上一层值。
    /// </remarks>
    public static IDisposable BeginScope(string? packageId)
    {
        var previous = _currentPackageId.Value;
        _currentPackageId.Value = packageId;
        return new RegistrationScope(previous);
    }

    private sealed class RegistrationScope(string? previous) : IDisposable
    {
        /// <summary>
        /// 释放作用域，将 <see cref="CurrentPackageId"/> 恢复为进入作用域前的上一层值。
        /// </summary>
        public void Dispose()
        {
            _currentPackageId.Value = previous;
        }
    }
}
