using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class MapBpPageViewModel : ObservableRecipient, IRecipient<HighlightMessage>
    {
        public ISharedDataService SharedDataService { get; }

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public MapBpPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public MapBpPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
            IsActive = true;
        }

        private Map? _pickedMap;

        public Map? PickedMap
        {
            get => _pickedMap;
            set
            {
                _pickedMap = value;
                SharedDataService.CurrentGame.PickedMap = _pickedMap;
                OnPropertyChanged();
            }
        }

        private Map? _bannedMap;

        public Map? BannedMap
        {
            get => _bannedMap;
            set
            {
                _bannedMap = value;
                SharedDataService.CurrentGame.BannedMap = _bannedMap;
                OnPropertyChanged();
            }
        }

        [ObservableProperty] 
        private bool _isPickHighlighted = false;
        
        [ObservableProperty]
        private bool  _isBanHighlighted = false;
        
        public void Receive(HighlightMessage message)
        {
            IsPickHighlighted = message.GameAction == GameAction.PickMap;
            IsBanHighlighted = message.GameAction == GameAction.BanMap;
        }

        public List<MapSelection> PickedMapSelections { get; } =
        [
            new(Map.军工厂, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "军工厂")),
            new(Map.红教堂, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "红教堂")),
            new(Map.圣心医院, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "圣心医院")),
            new(Map.里奥的回忆, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "里奥的回忆")),
            new(Map.月亮河公园, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "月亮河公园")),
            new(Map.湖景村, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "湖景村")),
            new(Map.永眠镇, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "永眠镇")),
            new(Map.唐人街, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "唐人街")),
            new(Map.不归林, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "不归林")),
        ];
        
        public class MapSelection(Map map, ImageSource? imageSource)
        {
            public Map Map { get; set; } = map;
            public ImageSource? ImageSource { get; set; } = imageSource;    
        }
    }
}
