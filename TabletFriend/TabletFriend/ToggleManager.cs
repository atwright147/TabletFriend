using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using WindowsInput;
using WindowsInput.Events;
using WindowsInput.Events.Sources;
using WindowsInput.Native;

namespace TabletFriend
{
    public static class ToggleManager
    {
        internal static readonly InputStateService Input = new InputStateService(new KeyboardOutput());
        private static IKeyboardEventSource _keyboard;
        private static readonly Dictionary<KeyCode, List<ToggleButton>> Buttons = new Dictionary<KeyCode, List<ToggleButton>>();
        private static bool _disposed;

        public static void AddButton(KeyCode key, ToggleButton button)
        {
            if (!Buttons.TryGetValue(key, out var list)) Buttons[key] = list = new List<ToggleButton>();
            list.Add(button);
        }

        public static void ClearButtons() => Buttons.Clear();
        public static bool IsHeld(KeyCode key) => Input.IsHeld(key);

        public static void Init()
        {
            if (_keyboard != null || _disposed) return;
            Input.Changed += OnChanged;
            _keyboard = Capture.Global.KeyboardAsync();
            _keyboard.KeyEvent += KeyEvent;
        }

        public static Task Toggle(KeyCode key) => Input.ToggleAsync(key);

        private static void OnChanged(KeyCode key, bool active)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (_disposed || dispatcher == null || dispatcher.HasShutdownStarted) return;
            dispatcher.BeginInvoke(new Action(() =>
            {
                if (_disposed || !Buttons.TryGetValue(key, out var list)) return;
                // Read current state, since several transitions may have queued callbacks.
                foreach (var button in list) button.IsChecked = Input.IsHeld(key);
            }));
        }

        private static void KeyEvent(object sender, EventSourceEventArgs<KeyboardEvent> e)
        {
            var key = e.Data?.KeyUp?.Key;
            var injected = e.RawData is GlobalKeyboardEventSourceCallbackData raw &&
                (raw.Data.Flags & (KeyboardHookStructFlags.Injected | KeyboardHookStructFlags.Injected_FromLower)) != 0;
            if (!_disposed && key.HasValue) Input.ObserveKeyEvent(key.Value, true, injected);
        }

        public static void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Input.Changed -= OnChanged;
            if (_keyboard != null)
            {
                _keyboard.KeyEvent -= KeyEvent;
                (_keyboard as IDisposable)?.Dispose();
            }
            Buttons.Clear();
        }
    }
}
