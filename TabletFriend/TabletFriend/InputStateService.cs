using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput;
using WindowsInput.Events;

namespace TabletFriend
{
    internal interface IKeyboardOutput
    {
        Task HoldAsync(KeyCode[] keys);
        Task ReleaseAsync(KeyCode[] keys);
    }

    internal sealed class KeyboardOutput : IKeyboardOutput
    {
        public async Task HoldAsync(KeyCode[] keys) => await Simulate.Events().Hold(keys).Invoke();
        public async Task ReleaseAsync(KeyCode[] keys) => await Simulate.Events().Release(keys).Invoke();
    }

    internal sealed class InputStateService
    {
        private readonly object _gate = new object();
        private readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
        private readonly HashSet<KeyCode> _legacyHeld = new HashSet<KeyCode>();
        private readonly Dictionary<object, HashSet<KeyCode>> _owners = new Dictionary<object, HashSet<KeyCode>>();
        private readonly SemaphoreSlim _serial = new SemaphoreSlim(1, 1);
        private readonly IKeyboardOutput _output;
        private int _simulating;
        private bool _stopping;
        private Task _releaseAll;
        public event Action<KeyCode, bool> Changed;

        public InputStateService(IKeyboardOutput output) { _output = output; }
        public bool SimulationInProgress => Volatile.Read(ref _simulating) != 0;
        public bool IsHeld(KeyCode key) { lock (_gate) return _held.Contains(key); }
        public void BeginShutdown() { lock (_gate) _stopping = true; }

        public Task HoldAsync(params KeyCode[] keys) => ChangeAsync(keys, true, false);
        public Task ReleaseAsync(params KeyCode[] keys) => ChangeAsync(keys, false, false);
        public Task ToggleAsync(KeyCode key) => ChangeAsync(new[] { key }, false, true);

        public bool IsOwned(object owner, KeyCode[] keys)
        {
            lock (_gate) return _owners.TryGetValue(owner, out var owned) && keys.All(owned.Contains);
        }

        // Each gesture control owns its contribution separately from legacy hold/toggle actions.
        public async Task SetOwnedAsync(object owner, KeyCode[] keys, bool hold)
        {
            await _serial.WaitAsync().ConfigureAwait(false);
            KeyCode[] changed = Array.Empty<KeyCode>();
            try
            {
                lock (_gate)
                {
                    if (_stopping) return;
                    changed = keys.Distinct().ToArray();
                    if (hold)
                    {
                        _owners[owner] = new HashSet<KeyCode>(changed);
                        keys = changed.Where(key => !_held.Contains(key)).ToArray();
                        foreach (var key in keys) _held.Add(key);
                    }
                    else
                    {
                        _owners.Remove(owner);
                        keys = changed.Where(key => _held.Contains(key) && !HasOwner(key)).Reverse().ToArray();
                    }
                    Interlocked.Increment(ref _simulating);
                }
                try
                {
                    if (keys.Length > 0)
                    {
                        if (hold) await _output.HoldAsync(keys).ConfigureAwait(false);
                        else await _output.ReleaseAsync(keys).ConfigureAwait(false);
                    }
                    lock (_gate)
                        if (!hold) foreach (var key in keys) _held.Remove(key);
                }
                finally
                {
                    Interlocked.Decrement(ref _simulating);
                    foreach (var key in changed) Changed?.Invoke(key, IsHeld(key));
                }
            }
            finally { _serial.Release(); }
        }

        // Called under _gate. _held also retains failed injections for shutdown recovery.
        private bool HasOwner(KeyCode key) => _legacyHeld.Contains(key) || _owners.Values.Any(keys => keys.Contains(key));

        private async Task ChangeAsync(KeyCode[] keys, bool hold, bool toggle)
        {
            var changed = keys;
            await _serial.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (_gate)
                {
                    if (_stopping) return;
                    if (toggle) hold = !_held.Contains(keys[0]);
                    if (hold) foreach (var key in keys) _legacyHeld.Add(key);
                    else
                    {
                        foreach (var key in keys) _legacyHeld.Remove(key);
                        keys = keys.Where(key => !HasOwner(key)).ToArray();
                    }
                    // Claim before injection: a failed call may have sent some key-downs.
                    if (hold) foreach (var key in keys) _held.Add(key);
                    Interlocked.Increment(ref _simulating);
                }
                try
                {
                    if (keys.Length > 0)
                    {
                        if (hold) await _output.HoldAsync(keys).ConfigureAwait(false);
                        else await _output.ReleaseAsync(keys).ConfigureAwait(false);
                    }
                    lock (_gate)
                        if (!hold) foreach (var key in keys) _held.Remove(key);
                }
                finally
                {
                    Interlocked.Decrement(ref _simulating);
                    foreach (var key in changed) Changed?.Invoke(key, IsHeld(key));
                }
            }
            finally { _serial.Release(); }
        }

        public void ObservePhysicalKeyUp(KeyCode key)
            => ObserveKeyEvent(key, true, false);

        public void ObserveKeyEvent(KeyCode key, bool isKeyUp, bool isInjected)
        {
            if (!isKeyUp || isInjected) return;
            lock (_gate)
            {
                if (SimulationInProgress || !_held.Remove(key)) return;
                _legacyHeld.Remove(key);
                foreach (var owned in _owners.Values) owned.Remove(key);
            }
            Changed?.Invoke(key, false);
        }

        public Task ReleaseAllAsync()
        {
            lock (_gate)
            {
                _stopping = true;
                return _releaseAll ??= ReleaseAllCoreAsync();
            }
        }

        private async Task ReleaseAllCoreAsync()
        {
            await _serial.WaitAsync().ConfigureAwait(false);
            var errors = new List<Exception>();
            try
            {
                KeyCode[] keys;
                lock (_gate) keys = _held.ToArray();
                foreach (var key in keys)
                {
                    Interlocked.Increment(ref _simulating);
                    try
                    {
                        await _output.ReleaseAsync(new[] { key }).ConfigureAwait(false);
                        lock (_gate)
                        {
                            _held.Remove(key);
                            _legacyHeld.Remove(key);
                            foreach (var owned in _owners.Values) owned.Remove(key);
                        }
                        Changed?.Invoke(key, false);
                    }
                    catch (Exception e) { errors.Add(e); }
                    finally { Interlocked.Decrement(ref _simulating); }
                }
            }
            finally { _serial.Release(); }
            if (errors.Count > 0) throw new AggregateException("Some held keys could not be released.", errors);
        }
    }
}
