using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Tests.Infrastructure;

/// <summary>
/// 为 WPF 测试提供有边界的 STA 线程执行助手。
/// </summary>
public static class WpfTestThread
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 在后台 STA 线程上运行同步 WPF 测试操作，若未在限定时间内完成则快速失败。
    /// </summary>
    /// <param name="action">要在 STA 线程上执行的操作。</param>
    /// <param name="timeout">在判定测试失败前等待的最长时间。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="action"/> 为 null 时抛出。</exception>
    /// <exception cref="TimeoutException">当操作未在 <paramref name="timeout"/> 之前完成时抛出。</exception>
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
    /// 在带 Dispatcher 消息泵的后台 STA 线程上运行异步 WPF 测试操作。
    /// </summary>
    /// <param name="action">要在 STA 线程上执行的异步操作。</param>
    /// <param name="timeout">在判定测试失败前等待的最长时间。</param>
    /// <returns>在操作完成时完成的任务。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="action"/> 为 null 时抛出。</exception>
    /// <exception cref="TimeoutException">当操作未在 <paramref name="timeout"/> 之前完成时抛出。</exception>
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
    /// 清理由先前测试遗留的孤立 WPFLocalizeExtension <c>DictionaryEvent</c> 监听器订阅，
    /// 这些订阅所属的 STA dispatcher 已经关闭。当新测试加载 XAML 并设置
    /// <c>ResxLocalizationProvider.DefaultAssembly</c> 时，该 setter 会触发
    /// <c>DictionaryEvent.Invoke</c>，通知所有已注册的监听器。来自先前测试的孤立
    /// 监听器仍引用已死亡的 dispatcher，会在 <c>LoadBaml</c> 期间引发
    /// <c>TaskCanceledException</c>（被包装为 <c>XamlParseException</c>）。
    /// 在每次测试之前移除所有监听器可确保只有当前测试的监听器处于活动状态。
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
