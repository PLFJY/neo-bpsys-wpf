using neo_bpsys_wpf.ProductTour;
namespace neo_bpsys_wpf.Views.Pages;

public partial class BanHunPage : ITutorialOwner<BanHunPage>
{
    /// <summary>监管者 Ban 页面教程键。</summary>
    public const string TutorialPageKey = "Page.Bp.BanHun";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>
    /// 注册监管者 Ban 页面拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<BanHunPage>()
            .Use(BanSurPage.Tours.CharacterSelectorBasic);
    }
}
