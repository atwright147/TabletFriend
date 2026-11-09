using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Threading;
using Xunit;

namespace TabletFriend.Tests
{
    public class AppFocusMonitorTests
    {
        [Fact]
        public void RegistrationAndShutdownCanCallBackIntoUiWithoutBlockingIt()
        {
            Sta.Run(async () =>
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                var registered = NewSource();
                var uiThread = Environment.CurrentManagedThreadId;
                var registrationThread = 0;
                var removals = 0;
                AutomationFocusChangedEventHandler handler = null;
                using var monitor = new AppFocusMonitor(add =>
                {
                    registrationThread = Environment.CurrentManagedThreadId;
                    Assert.NotEqual(uiThread, registrationThread);
                    Assert.Equal(ApartmentState.MTA, Thread.CurrentThread.GetApartmentState());
                    handler = add;
                    dispatcher.Invoke(() => registered.SetResult(true));
                }, remove =>
                {
                    Assert.Equal(registrationThread, Environment.CurrentManagedThreadId);
                    Assert.Equal(handler, remove);
                    dispatcher.Invoke(() => removals++);
                });

                await registered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(monitor.Completion.IsCompleted);
                monitor.Dispose();
                monitor.Dispose();
                await monitor.Completion.WaitAsync(TimeSpan.FromSeconds(5));
                monitor.Dispose();
                Assert.Equal(1, removals);
            });
        }

        [Fact]
        public async Task ShutdownDuringRegistrationWaitsForRemoval()
        {
            var entered = NewSource();
            using var finishRegistration = new ManualResetEventSlim();
            var removals = 0;
            using var monitor = new AppFocusMonitor(_ =>
            {
                entered.SetResult(true);
                Assert.True(finishRegistration.Wait(TimeSpan.FromSeconds(5)), "Registration was not released.");
            }, _ => removals++);

            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                monitor.Dispose();
                Assert.False(monitor.Completion.IsCompleted);
            }
            finally { finishRegistration.Set(); }
            await monitor.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, removals);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HookFailuresAreReportedThroughCompletion(bool failRegistration)
        {
            var failure = new InvalidOperationException("Focus hook failed");
            var removals = 0;
            using var monitor = new AppFocusMonitor(_ =>
            {
                if (failRegistration) throw failure;
            }, _ =>
            {
                removals++;
                throw failure;
            });

            monitor.Dispose();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => monitor.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Same(failure, error);
            Assert.Equal(failRegistration ? 0 : 1, removals);
        }

        [Fact]
        public async Task ForegroundWindowProbeDetectsAppsWithoutAutomationFocusEvents()
        {
            string foreground = null;
            var clipStudioSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var editorSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var monitor = new AppFocusMonitor(_ => { }, _ => { },
                () => Volatile.Read(ref foreground), TimeSpan.FromMilliseconds(10));
            monitor.OnAppChanged += app =>
            {
                if (app == "CLIPStudioPaint") clipStudioSeen.TrySetResult(true);
                if (app == "Code") editorSeen.TrySetResult(true);
            };

            Volatile.Write(ref foreground, "CLIPStudioPaint");
            await clipStudioSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("CLIPStudioPaint", monitor.FocusedApp);

            Volatile.Write(ref foreground, "Code");
            await editorSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("Code", monitor.FocusedApp);

            monitor.Dispose();
            await monitor.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }

        private static TaskCompletionSource<bool> NewSource()
            => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
