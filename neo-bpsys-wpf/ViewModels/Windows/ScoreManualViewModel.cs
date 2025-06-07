using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class ScoreManualWindowViewModel : ObservableObject
    {
        private readonly ISharedDataService _sharedDataService;
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public ScoreManualWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ScoreManualWindowViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
        }

        [RelayCommand]
        private void EditMainWin(int diff)
        {
            _sharedDataService.MainTeam.Score.Win += diff;
        }

        [RelayCommand]
        private void EditMainTie(int diff)
        {
            _sharedDataService.MainTeam.Score.Tie += diff;
        }

        [RelayCommand]
        private void EditMainMinorPoints(int diff)
        {
            _sharedDataService.MainTeam.Score.MinorPoints += diff;
        }

        [RelayCommand]
        private void EditAwayWin(int diff)
        {
            _sharedDataService.AwayTeam.Score.Win += diff;
        }

        [RelayCommand]
        private void EditAwayTie(int diff)
        {
            _sharedDataService.AwayTeam.Score.Tie += diff;
        }

        [RelayCommand]
        private void EditAwayMinorPoints(int diff)
        {
            _sharedDataService.AwayTeam.Score.MinorPoints += diff;
        }

        [RelayCommand]
        private void ClearMinorPoints()
        {
            _sharedDataService.MainTeam.Score.MinorPoints = 0;
            _sharedDataService.AwayTeam.Score.MinorPoints = 0;
        }
    }
}
