using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public class BanCountChangedEventArgs(BanListName banListName, int index) : EventArgs
{
    public BanListName BanListName { get; } = banListName;
    public int Index { get; } = index;
}