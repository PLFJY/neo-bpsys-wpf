using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class BanHunPage
{
    /// <summary>Hunter ban page tutorial key.</summary>
    public const string TutorialPageKey = "Page.Bp.BanHun";

    /// <summary>
    /// Registers tutorials owned by the hunter ban page.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey, [BanSurPage.TutorialPackages.CharacterSelectorBasic]);
    }
}
