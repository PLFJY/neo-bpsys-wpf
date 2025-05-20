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
    public partial class Step : ObservableObject
    {
        public Step(Enums.Action thisAction, int index)
        {
            ThisAction = thisAction;
            Index = index;
        }

        public Enums.Action ThisAction { get; private set; }
        public int Index { get; private set; }
    }
}