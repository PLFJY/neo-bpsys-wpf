using System.Globalization;

namespace neo_bpsys_wpf.Core.Events;

public class LanguageChangedEventArgs(CultureInfo cultureInfo) : EventArgs
{
    public CultureInfo CultureInfo { get; } = cultureInfo;
}