using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TalentPageViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public TalentPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        public TalentPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
        }
    }
}
