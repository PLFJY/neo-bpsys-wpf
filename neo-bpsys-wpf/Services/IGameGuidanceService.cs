using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace neo_bpsys_wpf.Services
{
    public interface IGameGuidanceService
    {
        public GameProgress CurrentGameProgress { get; set; }
    }
}