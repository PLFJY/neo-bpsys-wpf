namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 为设计器 v3 行为目标创建稳定的标识符。
/// </summary>
public static class FrontedBehaviorGuidHelper
{
    /// <summary>
    /// 为行为系统标识创建非空 GUID。
    /// </summary>
    public static Guid NewGuid()
    {
        var guid = Guid.CreateVersion7();
        return guid == Guid.Empty ? Guid.NewGuid() : guid;
    }
}

