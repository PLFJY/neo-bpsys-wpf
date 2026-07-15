using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class DefaultFrontedBindingRootProvider : IFrontedBindingRootProvider
{
    public IReadOnlyList<FrontedBindingRootDescriptor> GetRoots() =>
    [
        new("CurrentGame", typeof(Game)),
        new("HomeTeam", typeof(Team)),
        new("AwayTeam", typeof(Team)),
        new("RemainingSeconds", typeof(string)),
        new("CountDownRemainingSeconds", typeof(int)),
        new("CountDownTotalSeconds", typeof(int)),
        new("CanCurrentSurBannedList", typeof(ObservableCollection<bool>), FixedCount: AppConstants.CurrentBanSurCount),
        new("CanCurrentHunBannedList", typeof(ObservableCollection<bool>), FixedCount: AppConstants.CurrentBanHunCount),
        new("CanGlobalSurBannedList", typeof(ObservableCollection<bool>), FixedCount: AppConstants.GlobalBanSurCount),
        new("CanGlobalHunBannedList", typeof(ObservableCollection<bool>), FixedCount: AppConstants.GlobalBanHunCount)
    ];
}
