using System;
using System.Threading;
using System.Threading.Tasks;

namespace TabletFriend
{
    internal sealed class ReloadCoordinator<T> : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Func<CancellationToken, Task<T>> _read;
        private readonly Func<Action, Task> _dispatch;
        private readonly Action<T> _publish;
        private readonly Action<Exception> _report;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private CancellationTokenSource _debounce;
        private long _version;
        private bool _running;
        private bool _disposed;
        private Task _completion = Task.CompletedTask;

        public ReloadCoordinator(Func<CancellationToken, Task<T>> read, Func<Action, Task> dispatch,
            Action<T> publish, Action<Exception> report,
            Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _read = read; _dispatch = dispatch; _publish = publish; _report = report;
            _delay = delay ?? Task.Delay;
        }

        public Task Completion { get { lock (_gate) return _completion; } }

        public void Notify()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _version++;
                _debounce?.Cancel();
                if (_running) return;
                _running = true;
                _completion = Task.Run(RunAsync);
            }
        }

        private async Task RunAsync()
        {
            while (true)
            {
                long version;
                CancellationTokenSource debounce;
                lock (_gate)
                {
                    if (_disposed) { _running = false; return; }
                    version = _version;
                    debounce = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                    _debounce = debounce;
                }
                try
                {
                    await _delay(TimeSpan.FromMilliseconds(250), debounce.Token).ConfigureAwait(false);
                    debounce.Token.ThrowIfCancellationRequested();
                    // New changes invalidate this read, but do not start a concurrent one.
                    var result = await _read(_lifetime.Token).ConfigureAwait(false);
                    await _dispatch(() =>
                    {
                        lock (_gate)
                        {
                            if (!_disposed && version == _version) _publish(result);
                        }
                    }).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (debounce.IsCancellationRequested || _lifetime.IsCancellationRequested) { }
                catch (Exception e)
                {
                    await _dispatch(() =>
                    {
                        lock (_gate)
                        {
                            if (!_disposed && version == _version) _report(e);
                        }
                    }).ConfigureAwait(false);
                }
                finally
                {
                    lock (_gate)
                    {
                        if (_debounce == debounce) _debounce = null;
                        debounce.Dispose();
                    }
                }
                lock (_gate)
                {
                    if (_disposed || version == _version) { _running = false; return; }
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _lifetime.Cancel();
                _debounce?.Cancel();
                _ = _completion.ContinueWith(_ => _lifetime.Dispose(), TaskScheduler.Default);
            }
        }
    }
}
