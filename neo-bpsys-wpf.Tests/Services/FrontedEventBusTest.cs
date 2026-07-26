using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedEventBusTest
{
    [Fact]
    public async Task EventBus_Publish_NotifiesSubscribers()
    {
        var bus = new FrontedEventBus();
        using var semaphore = new SemaphoreSlim(0, 1);
        FrontedBehaviorEvent? received = null;

        using (bus.Subscribe(null, ev =>
        {
            received = ev;
            semaphore.Release();
            return Task.CompletedTask;
        }))
        {
            bus.Publish(new FrontedBehaviorEvent { EventType = "test.event" });
            Assert.True(await semaphore.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }

        Assert.NotNull(received);
        Assert.Equal("test.event", received!.EventType);
    }

    [Fact]
    public void EventBus_FilteredSubscribe_ReceivesOnlyMatchingEvent()
    {
        var bus = new FrontedEventBus();
        var receivedEvents = new List<string>();
        var gate = new object();

        using (bus.Subscribe("expected.event", ev =>
        {
            lock (gate) receivedEvents.Add(ev.EventType);
            return Task.CompletedTask;
        }))
        {
            bus.Publish(new FrontedBehaviorEvent { EventType = "expected.event" });
            bus.Publish(new FrontedBehaviorEvent { EventType = "other.event" });
        }

        Assert.Single(receivedEvents);
        Assert.Equal("expected.event", receivedEvents[0]);
    }

    [Fact]
    public void EventBus_HandlerException_DoesNotStopOtherHandlers()
    {
        var bus = new FrontedEventBus();
        var receivedEvents = new List<string>();
        var gate = new object();

        using (bus.Subscribe(null, ev =>
        {
            throw new InvalidOperationException("First handler fails");
        }))
        using (bus.Subscribe(null, ev =>
        {
            lock (gate) receivedEvents.Add(ev.EventType);
            return Task.CompletedTask;
        }))
        {
            bus.Publish(new FrontedBehaviorEvent { EventType = "test.event" });
        }

        Assert.Single(receivedEvents);
        Assert.Equal("test.event", receivedEvents[0]);
    }

    [Fact]
    public void EventBus_Unsubscribe_StopsReceiving()
    {
        var bus = new FrontedEventBus();
        var callCount = 0;

        var subscription = bus.Subscribe(null, ev =>
        {
            Interlocked.Increment(ref callCount);
            return Task.CompletedTask;
        });

        bus.Publish(new FrontedBehaviorEvent { EventType = "first" });
        Assert.Equal(1, callCount);

        subscription.Dispose();

        bus.Publish(new FrontedBehaviorEvent { EventType = "second" });
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task EventBus_AsyncHandlerException_IsObservedAndDoesNotStopOtherHandlers()
    {
        var logger = new RecordingLogger<FrontedEventBus>();
        var bus = new FrontedEventBus(logger);
        var secondHandlerCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using (bus.Subscribe(null, async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("Async handler fails");
        }))
        using (bus.Subscribe(null, _ =>
        {
            secondHandlerCalled.SetResult();
            return Task.CompletedTask;
        }))
        {
            bus.Publish(new FrontedBehaviorEvent { EventType = "test.event" });
            await secondHandlerCalled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await logger.WaitForExceptionAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.Contains(logger.Exceptions, ex => ex is AggregateException aggregate &&
            aggregate.InnerExceptions.Any(inner => inner is InvalidOperationException));
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly TaskCompletionSource _exceptionLogged = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<Exception> Exceptions { get; } = [];

        public IDisposable BeginScope<TState>(TState state) => new NoopDisposable();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (exception is null)
            {
                return;
            }

            Exceptions.Add(exception);
            _exceptionLogged.TrySetResult();
        }

        public Task WaitForExceptionAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            _exceptionLogged.Task.WaitAsync(timeout, cancellationToken);

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
