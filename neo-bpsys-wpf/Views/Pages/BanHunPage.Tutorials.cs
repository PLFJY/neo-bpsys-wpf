using neo_bpsys_wpf.ProductTour;
namespace neo_bpsys_wpf.Views.Pages;

public partial class BanHunPage : ITutorialOwner<BanHunPage>
{
    /// <summary>Hunter ban page tutorial key.</summary>
    public const string TutorialPageKey = "Page.Bp.BanHun";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>
    /// Registers tutorials owned by the hunter ban page.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<BanHunPage>()
            .Use(BanSurPage.Tours.CharacterSelectorBasic);
    }
}
