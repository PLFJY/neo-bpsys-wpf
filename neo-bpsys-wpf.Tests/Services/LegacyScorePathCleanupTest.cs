using System;
using System.IO;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class LegacyScorePathCleanupTest
{
    [Fact]
    public void ScorePageDoesNotUseLegacyTeamScoreAuthoringPaths()
    {
        var viewModel = ReadRepoFile("neo-bpsys-wpf", "ViewModels", "Pages", "ScorePageViewModel.cs");
        var view = ReadRepoFile("neo-bpsys-wpf", "Views", "Pages", "ScorePage.xaml");

        Assert.DoesNotContain("Team.Score", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("IFrontedWindowService", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("WeakReferenceMessenger", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("GameGlobalInfoRecord", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("SetGlobalScore", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ResetGlobalScore", viewModel, StringComparison.Ordinal);

        Assert.DoesNotContain("Team.Score", view, StringComparison.Ordinal);
        Assert.DoesNotContain("GameGlobalInfoRecord", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ManualControlCommand", view, StringComparison.Ordinal);
        Assert.Contains("CurrentGame.MatchScore.HomeTotalMinorScore", view, StringComparison.Ordinal);
        Assert.Contains("CurrentGame.MatchScore.AwayTotalMinorScore", view, StringComparison.Ordinal);
    }

    [Fact]
    public void ScoreWindowViewModelDoesNotUseLegacyTotalScoreMessengerPath()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "ViewModels", "Windows", "ScoreWindowViewModel.cs");

        Assert.DoesNotContain("PropertyChangedMessage<int>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TotalMainGameScore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TotalAwayGameScore", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontedWindowServiceOnlyKeepsDocumentedGlobalScoreCompatibilityAdapters()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Services", "FrontedWindowService.cs");

        Assert.DoesNotContain("_mainGlobalScoreControls", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_awayGlobalScoreControls", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GlobalScoreControlsReg", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoreGlobalControlToCanvas", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterScoreGlobalControl", source, StringComparison.Ordinal);
        Assert.Contains("Compatibility adapter", source, StringComparison.Ordinal);
        Assert.Contains("CurrentGame.MatchScore", source, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] pathParts) =>
        File.ReadAllText(Path.Combine(GetRepoRoot(), Path.Combine(pathParts)));

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
