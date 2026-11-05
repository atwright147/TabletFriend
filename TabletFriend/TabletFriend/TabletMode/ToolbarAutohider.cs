using System;
using System.Windows.Threading;

namespace TabletFriend.TabletMode
{
    public static class ToolbarAutohider
    {
        private static readonly DispatcherTimer Timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private static bool _firstRun = true;
        private static bool _oldValue;
        private static bool _disposed;

        static ToolbarAutohider() { Timer.Tick += OnElapsed; }

        private static void OnElapsed(object sender, EventArgs e)
        {
            if (_disposed || AppLifecycle.IsShuttingDown || !AppState.Settings.ToolbarAutohideEnabled) return;
            UpdateVisibility(TabletModeDetector.IsTabletMode);
        }

        internal static void UpdateVisibility(bool tabletMode)
        {
            if (_disposed || AppLifecycle.IsShuttingDown || !AppState.Settings.ToolbarAutohideEnabled) return;
            var changed = tabletMode != _oldValue || _firstRun;
            _firstRun = false;
            _oldValue = tabletMode;
            if (changed) EventBeacon.SendEvent(tabletMode ? Events.Maximize : Events.Minimize);
        }

        public static void ApplySettings()
        {
            if (_disposed || AppLifecycle.IsShuttingDown) return;
            if (AppState.Settings.ToolbarAutohideEnabled) StartWatching();
            else
            {
                StopWatching();
                EventBeacon.SendEvent(Events.Maximize);
            }
        }

        public static void StopWatching() => Timer.Stop();
        public static void StartWatching()
        {
            if (_disposed) return;
            _firstRun = true;
            Timer.Start();
        }
        public static void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Timer.Stop();
            Timer.Tick -= OnElapsed;
        }
    }
}
