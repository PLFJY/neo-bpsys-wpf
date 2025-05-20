using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using System.Collections.ObjectModel;
using System.Windows;
using static neo_bpsys_wpf.Services.GameGuidanceService;

namespace neo_bpsys_wpf.Services
{
    public interface IGameGuidanceService
    {
        Step CurrentStep { get; set; }
        void StartGuidance();
        void NextStep();
        void PrevStep();
        void StopGuidance();
    }
}