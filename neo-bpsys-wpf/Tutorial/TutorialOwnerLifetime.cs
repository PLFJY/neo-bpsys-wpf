using System.Runtime.CompilerServices;
using System.Windows;

namespace neo_bpsys_wpf.Tutorial;

internal static class TutorialOwnerLifetime
{
    private static readonly ConditionalWeakTable<FrameworkElement, Lifetime> Lifetimes = new();

    public static CancellationToken GetToken(FrameworkElement owner) =>
        Lifetimes.GetValue(owner, static element => new Lifetime(element)).Token;

    private sealed class Lifetime
    {
        private CancellationTokenSource _source = new();

        public Lifetime(FrameworkElement owner)
        {
            owner.Loaded += (_, _) =>
            {
                if (_source.IsCancellationRequested)
                {
                    _source.Dispose();
                    _source = new CancellationTokenSource();
                }
            };
            owner.Unloaded += (_, _) => _source.Cancel();
            if (owner is Window window)
            {
                window.Closed += (_, _) => _source.Cancel();
            }
        }

        public CancellationToken Token => _source.Token;
    }
}
