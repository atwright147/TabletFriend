using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TabletFriend.Actions;
using Xunit;

namespace TabletFriend.Tests
{
    public class ActionExecutionTests
    {
        [Fact]
        public async Task ShutdownCancelsWaitingBatchAndRejectsNewActions()
        {
            var errors = new List<Exception>();
            var runner = new ActionExecution(errors.Add);
            var steps = 0;
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var action = new BatchAction(new ButtonAction[] {
                new CallbackAction(() => { steps++; started.SetResult(true); }),
                new BatchAction(new ButtonAction[] { new WaitAction(-1), new CallbackAction(() => steps++) }),
                new CallbackAction(() => steps++)
            });
            var batch = runner.RunAsync(() => action.Invoke(runner.Token));
            await started.Task;
            await runner.StopAsync();
            await batch;
            await runner.RunAsync(() => { steps++; return Task.CompletedTask; });
            Assert.Equal(1, steps);
            Assert.Empty(errors);
        }

        [Fact]
        public async Task ReportsSynchronousAndAsynchronousActionFailures()
        {
            var errors = new List<Exception>();
            var runner = new ActionExecution(errors.Add);
            await runner.RunAsync(() => throw new InvalidOperationException("sync"));
            await runner.RunAsync(() => Task.FromException(new InvalidOperationException("async")));
            await runner.StopAsync();
            Assert.Equal(2, errors.Count);
        }

        private sealed class CallbackAction : ButtonAction
        {
            private readonly Action _callback;
            public CallbackAction(Action callback) { _callback = callback; }
            public override Task Invoke() { _callback(); return Task.CompletedTask; }
        }
    }
}
