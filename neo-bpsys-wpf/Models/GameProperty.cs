using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Security.Policy;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Extensions;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Models
{
    public partial class GameProperty : ObservableObject
    { 
        public GameProperty(int surCurrentBan, int hunCurrentBan, int surGlobalBan, int hunGlobalBan, Step[] stepArray)
        {
            SurCurrentBan = surCurrentBan;
            HunCurrentBan = hunCurrentBan;
            SurGlobalBan = surGlobalBan;
            HunGlobalBan = hunGlobalBan;
            StepArray = stepArray;
        }

        public int SurCurrentBan { get; private set; }
        public int HunCurrentBan { get; private set; }
        public int SurGlobalBan { get; private set; }
        public int HunGlobalBan { get; private set; }
        public Step[] StepArray { get; private set; }

    }
}