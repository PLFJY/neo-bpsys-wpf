using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class SettingPageViewModel : ObservableObject
    {
        public SettingPageViewModel()
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
            AppVersion = "版本 " + App.ResourceAssembly.GetName().Version!.ToString();
        }

        [ObservableProperty]
        private string _appVersion = string.Empty;
    }
}
