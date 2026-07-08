using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Tests built-in neo-bpsys-wpf tutorial registration contracts.
/// </summary>
public sealed class NeoBpsysTutorialRegistrationTest
{
    [Fact]
    public void RegistrationKeepsBuiltInSequencesPackagesAndFirstRunIncludes()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();

        NeoBpsysTutorialRegistration.Register(packageRegistry, sequenceRegistry, flowRegistry);

        Assert.Equal(
            [
                TutorialPackageIds.TeamInfoBasic,
                TutorialPackageIds.TeamInfoJsonImport,
                TutorialPackageIds.TeamInfoPlayerManage,
                TutorialPackageIds.TeamInfoAdvanced
            ],
            sequenceRegistry.GetSequence(TutorialPageKeys.TeamInfo));
        Assert.Equal(29, packageRegistry.GetPackages().Count);

        var firstRun = flowRegistry.GetFlow(TutorialFlowIds.FirstRunStandardBp);
        Assert.NotNull(firstRun);
        Assert.Equal(1, firstRun.Version);
        Assert.Equal(
            [
                TutorialPackageIds.FrontManageBpWindowLaunchBasic,
                TutorialPackageIds.GameManageBasic,
                TutorialPackageIds.TeamInfoBasic,
                TutorialPackageIds.TeamInfoJsonImport,
                TutorialPackageIds.TeamInfoPlayerManage,
                TutorialPackageIds.BpGameGuidanceBasic,
                TutorialPackageIds.BpGameGuidanceFlowBo1FirstHalf,
                TutorialPackageIds.BpSharedBasic,
                TutorialPackageIds.BpCharacterSelectorBasic,
                TutorialPackageIds.BpGlobalBanRecordBasic,
                TutorialPackageIds.ScoreBasic,
                TutorialPackageIds.GameManageGlobalBanCarryOver
            ],
            firstRun.IncludedPackageIds);
        Assert.Equal(firstRun.IncludedPackageIds.Count + 2, firstRun.Items.Count);
    }
}
