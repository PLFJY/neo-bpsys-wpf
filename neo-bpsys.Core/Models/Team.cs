using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys.Core.Enums;

namespace neo_bpsys.Core.Models;

public partial class Team : ObservableObject
{
    public Team(Camp camp, TeamType type)
    {
        Camp = camp;
        Type = type;
    }

    public Camp Camp { get; }
    public TeamType Type { get; }
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _logoPath;
    public Score Score { get; } = new();
}
