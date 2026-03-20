using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Events;

/// <summary>
/// 角色选择事件参数
/// </summary>
/// <param name="Camp">阵营</param>
/// <param name="PlayerIndex">玩家序号索引，监管者为 -1</param>
public record CharacterSelectedEventArgs(Camp Camp,int PlayerIndex);