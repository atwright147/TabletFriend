using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TabletFriend.Actions;

namespace TabletFriend
{
    internal sealed class ActionExecution
    {
        private readonly object _gate = new object();
        private readonly HashSet<Task> _running = new HashSet<Task>();
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private readonly Action<Exception> _report;
        private bool _stopping;
        public CancellationToken Token => _shutdown.Token;
        public ActionExecution(Action<Exception> report) { _report = report; }

        public Task RunAsync(Func<Task> invoke)
        {
            Task task;
            lock (_gate)
            {
                if (_stopping) return Task.CompletedTask;
                try { task = invoke(); }
                catch (Exception e) { task = Task.FromException(e); }
                _running.Add(task);
            }
            return ObserveAsync(task);
        }

        private async Task ObserveAsync(Task task)
        {
            try { await task; }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
            catch (Exception e) { _report(e); }
            finally { lock (_gate) _running.Remove(task); }
        }

        public async Task StopAsync()
        {
            Task[] tasks;
            lock (_gate)
            {
                _stopping = true;
                tasks = _running.ToArray();
            }
            _shutdown.Cancel();
            try { await Task.WhenAll(tasks); }
            catch { /* Each action is reported at its execution boundary. */ }
        }
    }

    internal static class ActionRunner
    {
        private static readonly ActionExecution Execution = new ActionExecution(
            error => AppLifecycle.ReportError("Action failed: " + error.Message));
        public static CancellationToken Token => Execution.Token;
        public static Task RunAsync(ButtonAction action) => Execution.RunAsync(() => action.Invoke(Token));
        public static Task StopAsync() => Execution.StopAsync();
    }
}
