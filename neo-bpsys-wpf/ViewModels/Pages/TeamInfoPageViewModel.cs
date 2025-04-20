using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TeamInfoPageViewModel : ObservableObject
    {
        public TeamInfoPageViewModel()
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        public TeamInfoPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
        }


        [RelayCommand]
        private void ConfirmTeamName(string team)
        {
            if (team == "Main")
                SharedDataService.MainTeam.Name = MainTeamName;
            else if (team == "Away")
                SharedDataService.AwayTeam.Name = AwayTeamName;
        }

        [RelayCommand]
        private void SetTeamLogo(string team)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.tif;*.tiff;*.svg;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.tif;*.tiff;*.svg;*.webp"
            };

            if (openFileDialog.ShowDialog() != true) return;

            var fileName = openFileDialog.FileName;

            if (team == "Main")
            {
                SharedDataService.MainTeam.Logo = new BitmapImage(new Uri(fileName));
                MainTeamLogo = SharedDataService.MainTeam.Logo;
            }
            else if (team == "Away")
            {
                SharedDataService.AwayTeam.Logo = new BitmapImage(new Uri(fileName));
                AwayTeamLogo = SharedDataService.AwayTeam.Logo;
            }
        }


        //主队信息
        [ObservableProperty]
        private string _mainTeamName = string.Empty;

        [ObservableProperty]
        private string _mainTeamCamp = $"当前状态：求生者";

        [ObservableProperty]
        private BitmapImage? _mainTeamLogo = null;

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
        private void AddMainSurPlayer()
        {
            MainSurPlayerEditorItems.Add(new PlayersEditorItemContent());
            RemoveMainSurPlayerCommandCanExecute = true;
            OnPropertyChanged(nameof(RemoveMainSurPlayerCommandCanExecute));
        }

        [RelayCommand]
        private void RemoveMainSurPlayer()
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
        private void AddMainHunPlayer()
        {
            MainHunPlayerEditorItems.Add(new PlayersEditorItemContent());
            RemoveMainHunPlayerCommandCanExecute = true;
            OnPropertyChanged(nameof(RemoveMainHunPlayerCommandCanExecute));
        }

        [RelayCommand]
        private void RemoveMainHunPlayer()
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

        // 客队信息编辑器

        [ObservableProperty]
        private string _awayTeamName = string.Empty;

        [ObservableProperty]
        private string _awayTeamCamp = "当前状态：监管者";

        [ObservableProperty]
        private BitmapImage? _awayTeamLogo = null;

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
        private void AddAwaySurPlayer()
        {
            AwaySurPlayerEditorItems.Add(new PlayersEditorItemContent());
            RemoveAwaySurPlayerCommandCanExecute = true;
            OnPropertyChanged(nameof(RemoveAwaySurPlayerCommandCanExecute));
        }

        [RelayCommand]
        private void RemoveAwaySurPlayer()
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
        private void AddAwayHunPlayer()
        {
            AwayHunPlayerEditorItems.Add(new PlayersEditorItemContent());
            RemoveAwayHunPlayerCommandCanExecute = true;
            OnPropertyChanged(nameof(RemoveAwayHunPlayerCommandCanExecute));
        }

        [RelayCommand]
        private void RemoveAwayHunPlayer()
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
        public partial class PlayersEditorItemContent : ObservableObject
        {
            public string PlayerName { get; set; }

            public BitmapImage? Image { get; set; }

            public PlayersEditorItemContent()
            {
                PlayerName = String.Empty;
            }

            public PlayersEditorItemContent(string playerName)
            {
                PlayerName = playerName;
            }

            [RelayCommand]
            private void SetPlayerImage()
            {
                Debug.WriteLine("Debug");
            }
        }
    }
}
