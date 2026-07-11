using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Tests.Infrastructure;

/// <summary>
/// Provides bounded STA-thread execution helpers for WPF tests.
/// </summary>
public static class WpfTestThread
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Runs a synchronous WPF test action on a background STA thread and fails quickly if it does not finish.
    /// </summary>
    /// <param name="action">The action to execute on the STA thread.</param>
    /// <param name="timeout">The maximum time to wait before failing the test.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the action does not finish before <paramref name="timeout"/>.</exception>
    public static void Run(Action action, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = CreateStaThread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                ClearOrphanedLocalizationEventSubscriptions();
                action();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                ShutdownDispatcher();
            }
        });

        thread.Start();
        WaitForCompletion(completion.Task, timeout ?? DefaultTimeout);
    }

    /// <summary>
    /// Runs an asynchronous WPF test action on a background STA thread with a Dispatcher message pump.
    /// </summary>
    /// <param name="action">The asynchronous action to execute on the STA thread.</param>
    /// <param name="timeout">The maximum time to wait before failing the test.</param>
    /// <returns>A task that completes when the action finishes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the action does not finish before <paramref name="timeout"/>.</exception>
    public static async Task RunAsync(Func<Task> action, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = CreateStaThread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                ClearOrphanedLocalizationEventSubscriptions();

                var frame = new DispatcherFrame();
                var task = action();
                task.ContinueWith(
                    completed =>
                    {
                        try
                        {
                            if (completed.IsFaulted)
                            {
                                completion.TrySetException(completed.Exception!.InnerExceptions);
                            }
                            else if (completed.IsCanceled)
                            {
                                completion.TrySetCanceled();
                            }
                            else
                            {
                                completion.TrySetResult();
                            }
                        }
                        finally
                        {
                            frame.Continue = false;
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.FromCurrentSynchronizationContext());

                Dispatcher.PushFrame(frame);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                ShutdownDispatcher();
            }
        });

        thread.Start();
        await completion.Task.WaitAsync(timeout ?? DefaultTimeout);
    }

    private static Thread CreateStaThread(ThreadStart start)
    {
        var thread = new Thread(start)
        {
            IsBackground = true,
            Name = "WPF test STA thread"
        };
        thread.SetApartmentState(ApartmentState.STA);
        return thread;
    }

    private static void WaitForCompletion(Task task, TimeSpan timeout)
    {
        try
        {
            task.WaitAsync(timeout).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"WPF test action did not finish within {timeout.TotalSeconds:N0} seconds.");
        }
    }

    /// <summary>
    /// Clears orphaned WPFLocalizeExtension <c>DictionaryEvent</c> listener subscriptions
    /// left by prior tests whose STA dispatchers have shut down.  When a new test loads XAML
    /// that sets <c>ResxLocalizationProvider.DefaultAssembly</c>, the setter raises
    /// <c>DictionaryEvent.Invoke</c> which notifies every registered listener.  Orphaned
    /// listeners from previous tests still reference dead dispatchers, causing
    /// <c>TaskCanceledException</c> (wrapped in <c>XamlParseException</c>) during
    /// <c>LoadBaml</c>.  Removing all listeners before each test ensures only the current
    /// test's listeners are active.
    /// </summary>
    private static void ClearOrphanedLocalizationEventSubscriptions()
    {
        try
        {
            var dictEventType = typeof(LocalizeDictionary)
                .GetNestedType("DictionaryEvent", BindingFlags.Public | BindingFlags.NonPublic);
            if (dictEventType is null)
            {
                return;
            }

            var listenerInterfaceType = typeof(LocalizeDictionary).Assembly
                .GetType("WPFLocalizeExtension.Engine.IDictionaryEventListener");
            if (listenerInterfaceType is null)
            {
                return;
            }

            var enumerateMethod = dictEventType.GetMethod(
                "EnumerateListeners",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var removeMethod = dictEventType.GetMethod(
                "RemoveListener",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (enumerateMethod is null || removeMethod is null)
            {
                return;
            }

            MethodInfo concreteEnumerate;
            if (enumerateMethod.IsGenericMethod)
            {
                concreteEnumerate = enumerateMethod.MakeGenericMethod(listenerInterfaceType);
            }
            else
            {
                concreteEnumerate = enumerateMethod;
            }

            if (concreteEnumerate.Invoke(null, null) is not System.Collections.IEnumerable listeners)
            {
                return;
            }

            var toRemove = new List<object>();
            foreach (var listener in listeners)
            {
                if (listener != null)
                {
                    toRemove.Add(listener);
                }
            }

            foreach (var listener in toRemove)
            {
                removeMethod.Invoke(null, new[] { listener });
            }
        }
        catch
        {
            // Best-effort cleanup; ignore if reflection fails.
        }
    }

    private static void ShutdownDispatcher()
    {
        try
        {
            Dispatcher.CurrentDispatcher.InvokeShutdown();
        }
        catch (InvalidOperationException)
        {
        }
    }
}
