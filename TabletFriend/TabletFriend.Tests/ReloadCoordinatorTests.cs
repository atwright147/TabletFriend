using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TabletFriend.Tests
{
    public class ReloadCoordinatorTests
    {
        [Fact]
        public async Task DebouncesBurstsAndDiscardsReadInvalidatedByANewerChange()
        {
            var clock = new ControlledDelay();
            var reads = new ConcurrentQueue<TaskCompletionSource<int>>();
            var started = new SemaphoreSlim(0);
            var published = new List<int>();
            var active = 0;
            var maximum = 0;
            using var reload = new ReloadCoordinator<int>(async token =>
            {
                maximum = Math.Max(maximum, Interlocked.Increment(ref active));
                var read = NewSource<int>();
                reads.Enqueue(read);
                started.Release();
                try { return await read.Task.WaitAsync(token); }
                finally { Interlocked.Decrement(ref active); }
            }, action => { action(); return Task.CompletedTask; }, published.Add,
                error => throw new Exception("Unexpected reload failure", error), clock.Delay);

            reload.Notify();
            var firstDelay = await clock.Next();
            reload.Notify();
            var secondDelay = await clock.Next();
            Assert.True(firstDelay.Task.IsCanceled);
            Assert.Empty(reads);
            secondDelay.TrySetResult(true);
            await started.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(reads.TryDequeue(out var firstRead));
            reload.Notify();
            firstRead.SetResult(1);
            var thirdDelay = await clock.Next();
            Assert.Empty(published);
            thirdDelay.SetResult(true);
            await started.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(reads.TryDequeue(out var secondRead));
            secondRead.SetResult(2);
            await reload.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { 2 }, published);
            Assert.Equal(1, maximum);
        }

        [Fact]
        public async Task DisposeCancelsDebounceAndIgnoresLaterNotifications()
        {
            var clock = new ControlledDelay();
            var reads = 0;
            var reload = new ReloadCoordinator<int>(_ => { reads++; return Task.FromResult(1); },
                action => { action(); return Task.CompletedTask; }, _ => Assert.Fail("Published after disposal"),
                _ => Assert.Fail("Unexpected error"), clock.Delay);
            reload.Notify();
            await clock.Next();
            reload.Dispose();
            reload.Notify();
            await reload.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, reads);
        }

        [Fact]
        public async Task DisposeBeforeDispatcherPublicationDiscardsResult()
        {
            var callback = NewSource<Action>();
            var dispatched = NewSource<bool>();
            var published = false;
            using var reload = new ReloadCoordinator<int>(_ => Task.FromResult(1), action =>
            {
                callback.SetResult(action);
                return dispatched.Task;
            }, _ => published = true, _ => Assert.Fail("Unexpected error"),
                (_, token) => Task.CompletedTask);
            reload.Notify();
            var publish = await callback.Task.WaitAsync(TimeSpan.FromSeconds(5));
            reload.Dispose();
            publish();
            dispatched.SetResult(true);
            await reload.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(published);
        }

        private static TaskCompletionSource<T> NewSource<T>() => new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        private sealed class ControlledDelay
        {
            private readonly ConcurrentQueue<TaskCompletionSource<bool>> _requests = new ConcurrentQueue<TaskCompletionSource<bool>>();
            private readonly SemaphoreSlim _available = new SemaphoreSlim(0);
            public async Task Delay(TimeSpan duration, CancellationToken token)
            {
                Assert.Equal(TimeSpan.FromMilliseconds(250), duration);
                var source = NewSource<bool>();
                using var registration = token.Register(() => source.TrySetCanceled(token));
                _requests.Enqueue(source);
                _available.Release();
                await source.Task;
            }
            public async Task<TaskCompletionSource<bool>> Next()
            {
                Assert.True(await _available.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.True(_requests.TryDequeue(out var request));
                return request;
            }
        }
    }
}
