namespace neo_bpsys_wpf.Abstractions.Services
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