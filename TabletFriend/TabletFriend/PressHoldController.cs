using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WindowsInput.Events;

namespace TabletFriend
{
    internal sealed class PressHoldController
    {
        private readonly InputStateService _input;
        private readonly KeyCode[] _keys;
        private readonly bool _quickToggle;
        private readonly int _thresholdMs;
        private readonly Func<TimeSpan> _now;
        private readonly object _owner = new object();
        private Task _pending = Task.CompletedTask;
        private Press _press;

        private sealed class Press
        {
            public TimeSpan Started;
            public bool WasLocked;
            public bool Failed;
        }

        public PressHoldController(InputStateService input, KeyCode[] keys, bool quickToggle,
            int thresholdMs, Func<TimeSpan> now = null)
        {
            _input = input;
            _keys = keys.Distinct().ToArray();
            _quickToggle = quickToggle;
            _thresholdMs = thresholdMs;
            _now = now ?? (() => TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency));
        }

        public bool IsActive => _keys.All(_input.IsHeld);

        // Invoked on the UI thread. Timestamp and gesture identity are captured before any await.
        public Task BeginAsync()
        {
            if (_press != null) return Task.CompletedTask;
            var press = _press = new Press { Started = _now() };
            return Enqueue(async () =>
            {
                press.WasLocked = _input.IsOwned(_owner, _keys);
                try { await _input.SetOwnedAsync(_owner, _keys, true); }
                catch (Exception error)
                {
                    press.Failed = true;
                    try { await _input.SetOwnedAsync(_owner, _keys, false); }
                    catch (Exception cleanupError) { throw new AggregateException(error, cleanupError); }
                    throw;
                }
            });
        }

        public Task EndAsync(bool inside)
        {
            var press = _press;
            if (press == null) return Task.CompletedTask;
            _press = null;
            var quick = inside && _quickToggle && (_now() - press.Started).TotalMilliseconds < _thresholdMs;
            return Enqueue(() => quick && !press.WasLocked && !press.Failed
                ? Task.CompletedTask : _input.SetOwnedAsync(_owner, _keys, false));
        }

        public Task CancelAsync()
        {
            _press = null;
            return Enqueue(() => _input.SetOwnedAsync(_owner, _keys, false));
        }

        private Task Enqueue(Func<Task> action)
        {
            var previous = _pending;
            return _pending = RunAfterAsync(previous, action);
        }

        private static async Task RunAfterAsync(Task previous, Func<Task> action)
        {
            try { await previous; }
            catch { /* The execution boundary reports each failure; cleanup must still run. */ }
            await action();
        }
    }
}
