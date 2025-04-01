using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TeamInfoPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _mainTeamState = "当前状态：求生者";

        [ObservableProperty]
        private string _awayTeamState = "当前状态：监管者";

        [ObservableProperty]
        private BitmapImage? _mainTeamLogo = null;

        [ObservableProperty]
        private BitmapImage? _awayTeamLogo = null;

        /// <summary>
        /// 主队信息编辑器
        /// </summary>

        //求生者
        public ObservableCollection<PlayersEditorItemContent> MainSurPlayerEditorItems { get; set; } = new()
        {
            new PlayersEditorItemContent(),
            new PlayersEditorItemContent(),
            new PlayersEditorItemContent(),
            new PlayersEditorItemContent(),
        };

        [ObservableProperty] private bool _removeMainSurPlayerCommandCanExecute = false;

        [RelayCommand]
        public void AddMainSurPlayer()
        {
            MainSurPlayerEditorItems.Add(new PlayersEditorItemContent());
            RemoveMainSurPlayerCommandCanExecute = true;
            OnPropertyChanged(nameof(RemoveMainSurPlayerCommandCanExecute));
        }

        [RelayCommand]
        public void RemoveMainSurPlayer()
        {
            if (MainSurPlayerEditorItems.Count > 4)
            {
                MainSurPlayerEditorItems.RemoveAt(MainSurPlayerEditorItems.Count - 1);
                OnPropertyChanged(nameof(MainSurPlayerEditorItems));
                if (MainSurPlayerEditorItems.Count == 4)
                {
                    RemoveMainSurPlayerCommandCanExecute = false;
                    OnPropertyChanged(nameof(RemoveMainSurPlayerCommandCanExecute));
                }

            }
            else
            {
                RemoveMainSurPlayerCommandCanExecute = false;
                OnPropertyChanged(nameof(RemoveMainSurPlayerCommandCanExecute));
            }
        }


        //监管者
        public ObservableCollection<PlayersEditorItemContent> MainHunPlayerEditorItems { get; set; } = new()
        {
            new PlayersEditorItemContent(),
        };

        [ObservableProperty] private bool _removeMainHunPlayerCommandCanExecute = false;

        [RelayCommand]
        public void AddMainHunPlayer()
        {
            MainHunPlayerEditorItems.Add(new PlayersEditorItemContent());
            RemoveMainHunPlayerCommandCanExecute = true;
            OnPropertyChanged(nameof(RemoveMainHunPlayerCommandCanExecute));
        }

        [RelayCommand]
        public void RemoveMainHunPlayer()
        {
            if (MainSurPlayerEditorItems.Count > 1)
            {
                MainHunPlayerEditorItems.RemoveAt(MainHunPlayerEditorItems.Count - 1);
                OnPropertyChanged(nameof(MainHunPlayerEditorItems));
                if (MainHunPlayerEditorItems.Count == 1)
                {
                    RemoveMainHunPlayerCommandCanExecute = false;
                    OnPropertyChanged(nameof(RemoveMainHunPlayerCommandCanExecute));
                }

            }
            else
            {
                RemoveMainHunPlayerCommandCanExecute = false;
                OnPropertyChanged(nameof(RemoveMainHunPlayerCommandCanExecute));
            }
        }

        /// <summary>
        /// 客队信息编辑器
        /// </summary>

        //求生者
        public ObservableCollection<PlayersEditorItemContent> AwaySurPlayerEditorItems { get; set; } = new()
        {
            new PlayersEditorItemContent(),
            new PlayersEditorItemContent(),
            new PlayersEditorItemContent(),
            new PlayersEditorItemContent(),
        };

        [ObservableProperty] private bool _removeAwaySurPlayerCommandCanExecute = false;

        [RelayCommand]
        public void AddAwaySurPlayer()
        {
            AwaySurPlayerEditorItems.Add(new PlayersEditorItemContent());
            RemoveAwaySurPlayerCommandCanExecute = true;
            OnPropertyChanged(nameof(RemoveAwaySurPlayerCommandCanExecute));
        }

        [RelayCommand]
        public void RemoveAwaySurPlayer()
        {
            if (AwaySurPlayerEditorItems.Count > 4)
            {
                AwaySurPlayerEditorItems.RemoveAt(AwaySurPlayerEditorItems.Count - 1);
                OnPropertyChanged(nameof(AwaySurPlayerEditorItems));
                if (AwaySurPlayerEditorItems.Count == 4)
                {
                    RemoveAwaySurPlayerCommandCanExecute = false;
                    OnPropertyChanged(nameof(RemoveAwaySurPlayerCommandCanExecute));
                }

            }
            else
            {
                RemoveAwaySurPlayerCommandCanExecute = false;
                OnPropertyChanged(nameof(RemoveAwaySurPlayerCommandCanExecute));
            }
        }


        //监管者
        public ObservableCollection<PlayersEditorItemContent> AwayHunPlayerEditorItems { get; set; } = new()
        {
            new PlayersEditorItemContent(),
        };

        [ObservableProperty] private bool _removeAwayHunPlayerCommandCanExecute = false;

        [RelayCommand]
        public void AddAwayHunPlayer()
        {
            AwayHunPlayerEditorItems.Add(new PlayersEditorItemContent());
            RemoveAwayHunPlayerCommandCanExecute = true;
            OnPropertyChanged(nameof(RemoveAwayHunPlayerCommandCanExecute));
        }

        [RelayCommand]
        public void RemoveAwayHunPlayer()
        {
            if (AwaySurPlayerEditorItems.Count > 1)
            {
                AwayHunPlayerEditorItems.RemoveAt(AwayHunPlayerEditorItems.Count - 1);
                OnPropertyChanged(nameof(AwayHunPlayerEditorItems));
                if (AwayHunPlayerEditorItems.Count == 1)
                {
                    RemoveAwayHunPlayerCommandCanExecute = false;
                    OnPropertyChanged(nameof(RemoveAwayHunPlayerCommandCanExecute));
                }

            }
            else
            {
                RemoveAwayHunPlayerCommandCanExecute = false;
                OnPropertyChanged(nameof(RemoveAwayHunPlayerCommandCanExecute));
            }
        }
    }
    public class PlayersEditorItemContent
    {
        public string PlayerName { get; set; }

        public SymbolIcon ButtonIcon { get; set; }

        public Brush ButtonBackground { get; set; }

        public string ButtonText { get; set; }

        public PlayersEditorItemContent()
        {
            PlayerName = String.Empty;
            ButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowUpload24 };
            ButtonBackground = new SolidColorBrush(Colors.DarkGreen);
            ButtonText = "上场";
        }

        public PlayersEditorItemContent(string playerName)
        {
            PlayerName = playerName;
            ButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowUpload24 };
            ButtonBackground = new SolidColorBrush(Colors.DarkGreen);
            ButtonText = "上场";
        }
    }
}
