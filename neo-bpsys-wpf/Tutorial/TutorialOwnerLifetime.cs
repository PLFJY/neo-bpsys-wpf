using System.Runtime.CompilerServices;
using System.Windows;

namespace neo_bpsys_wpf.Tutorial;

internal static class TutorialOwnerLifetime
{
    private static readonly ConditionalWeakTable<FrameworkElement, Lifetime> Lifetimes = new();

    public static CancellationToken GetToken(FrameworkElement owner) =>
        Lifetimes.GetValue(owner, static element => new Lifetime(element)).GetToken();

    private sealed class Lifetime
    {
        private readonly object _gate = new();
        private CancellationTokenSource _source = new();

        public Lifetime(FrameworkElement owner)
        {
            owner.Loaded += (_, _) => EnsureToken();
            owner.Unloaded += (_, _) => Cancel();
            if (owner is Window window)
            {
                window.Closed += (_, _) => Cancel();
            }
        }

        public CancellationToken GetToken() => EnsureToken();

        private CancellationToken EnsureToken()
        {
            lock (_gate)
            {
                // 页面可能先注册 Loaded 处理器再调用 GetToken；不能只依赖 Lifetime 自己的 Loaded 事件。
                if (_source.IsCancellationRequested)
                {
                    _source = new CancellationTokenSource();
                }

                return _source.Token;
            }
        }

        private void Cancel()
        {
            lock (_gate)
            {
                _source.Cancel();
            }
        }
    }
}
