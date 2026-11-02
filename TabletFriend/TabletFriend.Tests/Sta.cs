using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TabletFriend.Tests
{
    internal static class Sta
    {
        public static void Run(Func<Task> action) => Run(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var task = action().WaitAsync(TimeSpan.FromSeconds(12));
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
            task.GetAwaiter().GetResult();
        });

        public static void Run(Action action)
        {
            Exception error = null;
            var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "STA test timed out.");
            if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
