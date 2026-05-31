using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Builds the curated binding tree used by Designer v3 Binding Browser.
/// </summary>
public sealed class FrontedBindingBrowserProvider
{
    public IReadOnlyList<FrontedBindingTreeNode> BuildTree() =>
    [
        Node("ISharedDataService", null, typeof(ISharedDataService),
        [
            Node("CurrentGame", "CurrentGame", typeof(Game), BuildCurrentGameChildren()),
            Node("HomeTeam", "HomeTeam", typeof(Team), BuildTeamChildren("HomeTeam")),
            Node("AwayTeam", "AwayTeam", typeof(Team), BuildTeamChildren("AwayTeam")),
            Leaf("RemainingSeconds", "RemainingSeconds", typeof(string)),
            CollectionNode("CanCurrentSurBannedList", "CanCurrentSurBannedList", typeof(ObservableCollection<bool>), AppConstants.CurrentBanSurCount),
            CollectionNode("CanCurrentHunBannedList", "CanCurrentHunBannedList", typeof(ObservableCollection<bool>), AppConstants.CurrentBanHunCount),
            CollectionNode("CanGlobalSurBannedList", "CanGlobalSurBannedList", typeof(ObservableCollection<bool>), AppConstants.GlobalBanSurCount),
            CollectionNode("CanGlobalHunBannedList", "CanGlobalHunBannedList", typeof(ObservableCollection<bool>), AppConstants.GlobalBanHunCount)
        ])
    ];

    public IReadOnlyList<FrontedBindingTreeNode> Search(string? query)
    {
        var nodes = BuildTree().SelectMany(node => node.Flatten()).Where(node => node.IsSelectable);
        var filter = query?.Trim();
        if (string.IsNullOrWhiteSpace(filter))
        {
            return nodes.ToArray();
        }

        return nodes
            .Where(node =>
                node.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || node.FullPath?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true)
            .DistinctBy(node => node.FullPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<FrontedBindingTreeNode> BuildCurrentGameChildren() =>
    [
        Leaf("GameProgress", "CurrentGame.GameProgress", typeof(GameProgress)),
        Leaf("PickedMap", "CurrentGame.PickedMap", typeof(Map?)),
        Leaf("PickedMapImage", "CurrentGame.PickedMapImage", typeof(System.Windows.Media.ImageSource)),
        Leaf("PickedMapImageLarge", "CurrentGame.PickedMapImageLarge", typeof(System.Windows.Media.ImageSource)),
        Leaf("BannedMap", "CurrentGame.BannedMap", typeof(Map?)),
        Leaf("BannedMapImage", "CurrentGame.BannedMapImage", typeof(System.Windows.Media.ImageSource)),
        Node("SurTeam", "CurrentGame.SurTeam", typeof(Team), BuildTeamChildren("CurrentGame.SurTeam")),
        Node("HunTeam", "CurrentGame.HunTeam", typeof(Team), BuildTeamChildren("CurrentGame.HunTeam")),
        PlayerCollectionNode("SurPlayerList", "CurrentGame.SurPlayerList", AppConstants.CurrentBanSurCount),
        Node("HunPlayer", "CurrentGame.HunPlayer", typeof(Player), BuildPlayerChildren("CurrentGame.HunPlayer", includeSurvivorTalent: false)),
        Node("MatchScore", "CurrentGame.MatchScore", typeof(MatchScoreState), BuildMatchScoreChildren()),
        CharacterCollectionNode("CurrentSurBannedList", "CurrentGame.CurrentSurBannedList", AppConstants.CurrentBanSurCount),
        CharacterCollectionNode("CurrentHunBannedList", "CurrentGame.CurrentHunBannedList", AppConstants.CurrentBanHunCount),
        CharacterCollectionNode("SurTeam.GlobalBannedSurList", "CurrentGame.SurTeam.GlobalBannedSurList", AppConstants.GlobalBanSurCount),
        CharacterCollectionNode("HunTeam.GlobalBannedHunList", "CurrentGame.HunTeam.GlobalBannedHunList", AppConstants.GlobalBanHunCount)
    ];

    private static IReadOnlyList<FrontedBindingTreeNode> BuildTeamChildren(string prefix) =>
    [
        Leaf("Name", $"{prefix}.Name", typeof(string)),
        Leaf("Logo", $"{prefix}.Logo", typeof(System.Windows.Media.ImageSource))
    ];

    private static FrontedBindingTreeNode PlayerCollectionNode(string displayName, string prefix, int count) =>
        Node(displayName, prefix, typeof(ReadOnlyObservableCollection<Player>),
            Enumerable.Range(0, count)
                .Select(index => Node($"[{index}]", $"{prefix}[{index}]", typeof(Player), BuildPlayerChildren($"{prefix}[{index}]", includeSurvivorTalent: true)))
                .ToArray());

    private static IReadOnlyList<FrontedBindingTreeNode> BuildPlayerChildren(string prefix, bool includeSurvivorTalent)
    {
        var talentChildren = includeSurvivorTalent
            ? new[]
            {
                Leaf("BorrowedTime", $"{prefix}.Talent.BorrowedTime", typeof(bool)),
                Leaf("FlywheelEffect", $"{prefix}.Talent.FlywheelEffect", typeof(bool))
            }
            : new[]
            {
                Leaf("Detention", $"{prefix}.Talent.Detention", typeof(bool)),
                Leaf("TrumpCard", $"{prefix}.Talent.TrumpCard", typeof(bool))
            };

        return
        [
            Node("Member", $"{prefix}.Member", typeof(Member), [Leaf("Name", $"{prefix}.Member.Name", typeof(string))]),
            Node("Character", $"{prefix}.Character", typeof(Character),
            [
                Leaf("Name", $"{prefix}.Character.Name", typeof(string)),
                Leaf("HeaderImage", $"{prefix}.Character.HeaderImage", typeof(System.Windows.Media.ImageSource)),
                Leaf("HalfImage", $"{prefix}.Character.HalfImage", typeof(System.Windows.Media.ImageSource)),
                Leaf("BigImage", $"{prefix}.Character.BigImage", typeof(System.Windows.Media.ImageSource))
            ]),
            Leaf("PictureShown", $"{prefix}.PictureShown", typeof(System.Windows.Media.ImageSource)),
            Node("Talent", $"{prefix}.Talent", typeof(Talent), talentChildren),
            Leaf("Trait", $"{prefix}.Trait", typeof(Trait)),
            Node("Data", $"{prefix}.Data", typeof(PlayerData), BuildPlayerDataChildren($"{prefix}.Data", includeSurvivorTalent))
        ];
    }

    private static IReadOnlyList<FrontedBindingTreeNode> BuildPlayerDataChildren(string prefix, bool survivor) =>
        survivor
            ?
            [
                Leaf("DecodingProgress", $"{prefix}.DecodingProgress", typeof(string)),
                Leaf("PalletStrikes", $"{prefix}.PalletStrikes", typeof(string)),
                Leaf("Rescues", $"{prefix}.Rescues", typeof(string)),
                Leaf("Heals", $"{prefix}.Heals", typeof(string)),
                Leaf("ContainmentTime", $"{prefix}.ContainmentTime", typeof(string))
            ]
            :
            [
                Leaf("RemainingCipher", $"{prefix}.RemainingCipher", typeof(string)),
                Leaf("PalletsDestroyed", $"{prefix}.PalletsDestroyed", typeof(string)),
                Leaf("SurvivorHits", $"{prefix}.SurvivorHits", typeof(string)),
                Leaf("TerrorShocks", $"{prefix}.TerrorShocks", typeof(string)),
                Leaf("Knockdowns", $"{prefix}.Knockdowns", typeof(string))
            ];

    private static IReadOnlyList<FrontedBindingTreeNode> BuildMatchScoreChildren() =>
    [
        Leaf("CurrentSurTeamMajorText", "CurrentGame.MatchScore.CurrentSurTeamMajorText", typeof(string)),
        Leaf("CurrentHunTeamMajorText", "CurrentGame.MatchScore.CurrentHunTeamMajorText", typeof(string)),
        Leaf("CurrentSurTeamPreHalfMinorScoreText", "CurrentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText", typeof(string)),
        Leaf("CurrentHunTeamPreHalfMinorScoreText", "CurrentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText", typeof(string)),
        Leaf("HomeMajorText", "CurrentGame.MatchScore.HomeMajorText", typeof(string)),
        Leaf("AwayMajorText", "CurrentGame.MatchScore.AwayMajorText", typeof(string)),
        Leaf("HomeTotalMinorScore", "CurrentGame.MatchScore.HomeTotalMinorScore", typeof(int)),
        Leaf("AwayTotalMinorScore", "CurrentGame.MatchScore.AwayTotalMinorScore", typeof(int))
    ];

    private static FrontedBindingTreeNode CharacterCollectionNode(string displayName, string prefix, int count) =>
        Node(displayName, prefix, typeof(ObservableCollection<Character?>),
            Enumerable.Range(0, count)
                .Select(index => Node($"[{index}]", $"{prefix}[{index}]", typeof(Character),
                [
                    Leaf("Name", $"{prefix}[{index}].Name", typeof(string)),
                    Leaf("HeaderImage", $"{prefix}[{index}].HeaderImage", typeof(System.Windows.Media.ImageSource)),
                    Leaf("HalfImage", $"{prefix}[{index}].HalfImage", typeof(System.Windows.Media.ImageSource)),
                    Leaf("BigImage", $"{prefix}[{index}].BigImage", typeof(System.Windows.Media.ImageSource))
                ]))
                .ToArray());

    private static FrontedBindingTreeNode CollectionNode(string displayName, string prefix, Type type, int count) =>
        Node(displayName, prefix, type,
            Enumerable.Range(0, count)
                .Select(index => Leaf($"[{index}]", $"{prefix}[{index}]", typeof(bool)))
                .ToArray());

    private static FrontedBindingTreeNode Leaf(string displayName, string fullPath, Type type) =>
        new()
        {
            DisplayName = displayName,
            FullPath = fullPath,
            TypeName = type.Name
        };

    private static FrontedBindingTreeNode Node(
        string displayName,
        string? fullPath,
        Type type,
        IReadOnlyList<FrontedBindingTreeNode> children) =>
        new()
        {
            DisplayName = displayName,
            FullPath = fullPath,
            TypeName = type.Name,
            Children = children
        };
}
