using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Xunit;

namespace neo_bpsys_wpf.Tests.Resources;

public class ScoreWindowLayoutBindingTest
{
    [Fact]
    public void ScoreSurAndHunLayoutsBindScoreTextToMatchScoreState()
    {
        var surLayout = ReadLayout("neo-bpsys-wpf/Resources/FrontedLayouts/ScoreSurWindow/BaseCanvas.json");
        var hunLayout = ReadLayout("neo-bpsys-wpf/Resources/FrontedLayouts/ScoreHunWindow/BaseCanvas.json");

        Assert.Equal(
            "CurrentGame.MatchScore.CurrentSurTeamMajorText",
            GetBindingPath(surLayout, "SurTeamMajorPoint"));
        Assert.Equal(
            "CurrentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText",
            GetBindingPath(surLayout, "GameScoresSur"));
        Assert.Equal(
            "CurrentGame.MatchScore.CurrentHunTeamMajorText",
            GetBindingPath(hunLayout, "HunTeamMajorPoint"));
        Assert.Equal(
            "CurrentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText",
            GetBindingPath(hunLayout, "GameScoresHun"));
    }

    [Fact]
    public void ScoreSurAndHunLayoutsDoNotReferenceTeamScoreForScoreBindings()
    {
        var surLayoutText = ReadLayoutText("neo-bpsys-wpf/Resources/FrontedLayouts/ScoreSurWindow/BaseCanvas.json");
        var hunLayoutText = ReadLayoutText("neo-bpsys-wpf/Resources/FrontedLayouts/ScoreHunWindow/BaseCanvas.json");

        Assert.DoesNotContain("Team.Score", surLayoutText);
        Assert.DoesNotContain("Team.Score", hunLayoutText);
    }

    private static string GetBindingPath(JsonObject layout, string controlName) =>
        layout[controlName]?["BindingPath"]?.GetValue<string>()
        ?? throw new InvalidDataException($"Layout control '{controlName}' has no BindingPath.");

    private static JsonObject ReadLayout(string relativePath, [CallerFilePath] string sourceFilePath = "") =>
        JsonNode.Parse(ReadLayoutText(relativePath, sourceFilePath))?.AsObject()
        ?? throw new InvalidDataException($"Layout '{relativePath}' is not a JSON object.");

    private static string ReadLayoutText(string relativePath, [CallerFilePath] string sourceFilePath = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", ".."));
        return File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
    }
}
