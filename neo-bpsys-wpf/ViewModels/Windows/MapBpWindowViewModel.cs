using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class MapBpWindowViewModel : ObservableRecipient
    {
        private readonly ISharedDataService _sharedDataService;
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public MapBpWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public MapBpWindowViewModel(ISharedDataService sharedDataService)
        {
            IsActive = true;
            _sharedDataService = sharedDataService;
        }
    }
}
