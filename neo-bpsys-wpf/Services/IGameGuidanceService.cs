using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using System.Collections.ObjectModel;
using System.Windows;
using static neo_bpsys_wpf.Services.GameGuidanceService;

namespace neo_bpsys_wpf.Services
{
    public interface IGameGuidanceService
    {
        bool IsGuidanceStarted { get; set; }

        Task<string?> StartGuidance();
        Task<string> NextStepAsync();
        Task<string> PrevStepAsync();
        void StopGuidance();
    }
}