namespace neo_bpsys_wpf.Services.Abstractions;

/// <summary>
/// 管理 <c>.bpui</c> 布局包文件的 Windows 文件关联。
/// </summary>
public interface IBpuiFileAssociationService
{
    /// <summary>
    /// 判断 <c>.bpui</c> 文件当前是否与本应用程序关联。
    /// </summary>
    /// <returns>当前有效的关联指向本应用程序时返回 <see langword="true"/>。</returns>
    bool IsAssociated();

    /// <summary>
    /// 确保当前用户的 <c>.bpui</c> 文件关联指向本应用程序。
    /// </summary>
    void Associate();

    /// <summary>
    /// 如果当前用户的 <c>.bpui</c> 文件关联指向本应用程序，则移除该关联。
    /// </summary>
    void RemoveAssociation();

    /// <summary>
    /// 根据用户设置静默检查并修复文件关联。
    /// </summary>
    /// <param name="shouldAssociate">是否应启用关联。</param>
    void EnsureAssociationState(bool shouldAssociate);
}
