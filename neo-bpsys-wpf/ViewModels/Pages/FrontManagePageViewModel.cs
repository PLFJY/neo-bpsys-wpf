using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class FrontManagePageViewModel : ObservableObject
    {
        private readonly IFrontService _frontService;
        public FrontManagePageViewModel(IFrontService frontService)
        {
            _frontService = frontService;
        }

        [RelayCommand]
        public void ShowAllWinows()
        {
            _frontService.AllWindowShow();
        }

        [RelayCommand]
        public void HideAllWinows()
        {
            _frontService.AllWindowHide();
        }

        [RelayCommand]
        public void ShowBpWindow()
        {
            _frontService.BpWindowShow();
        }

        [RelayCommand]
        public void HideBpWindow()
        {
            _frontService.BpWindowHide();
        }

        [RelayCommand]
        public void ShowInterludeWindow()
        {
            _frontService.InterludeWindowShow();
        }

        [RelayCommand]
        public void HideInterludeWindow()
        {
            _frontService.InterludeWindowHide();
        }

        [RelayCommand]
        public void ShowScoreWindow()
        {
            _frontService.ScoreWindowShow();
        }

        [RelayCommand]
        public void HideScoreWindow()
        {
            _frontService.ScoreWindowHide();
        }

        [RelayCommand]
        public void ShowGameDataWindow()
        {
            _frontService.GameDataWindowShow();
        }

        [RelayCommand]
        public void HideGameDataWindow()
        {
            _frontService.GameDataWindowHide();
        }

        [RelayCommand]
        public void ShowWidgetsWindow()
        {
            _frontService.WidgetsWindowShow();
        }

        [RelayCommand]
        public void HideWidgetsWindow()
        {
            _frontService.WidgetsWindowHide();
        }

        private bool _isEditMode = false;

        public bool IsEditMode
        {
            get { return _isEditMode; }
            set { _isEditMode = value; }
        }
    }
}
