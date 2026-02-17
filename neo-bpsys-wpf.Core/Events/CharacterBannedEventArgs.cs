using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Events;

/// <summary>
/// 角色被禁止事件参数
/// </summary>
/// <param name="Camp">阵营</param>
/// <param name="PlayerIndex">玩家索引</param>
public record CharacterBannedEventArgs(Camp Camp, int PlayerIndex);