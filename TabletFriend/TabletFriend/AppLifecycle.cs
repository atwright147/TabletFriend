using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace TabletFriend
{
    internal static class AppLifecycle
    {
        private static MainWindow _window;
        private static Task _shutdown;
        public static bool IsShuttingDown { get; private set; }
        public static bool ShutdownComplete { get; private set; }

        public static void Initialize(MainWindow window) { _window = window; }

        public static Task ShutdownAsync()
        {
            if (_shutdown != null) return _shutdown;
            IsShuttingDown = true;
            ToggleManager.Input.BeginShutdown();
            var actions = ActionRunner.StopAsync();
            return _shutdown = ShutdownCoreAsync(actions);
        }

        private static async Task ShutdownCoreAsync(Task actions)
        {
            // Publish the shared task before cleanup can re-enter through window closing.
            await Task.Yield();
            var errors = new List<Exception>();
            Try(() => _window?.StopCallbacks(), errors);
            try { if (_window != null) await _window.WaitForCallbacksAsync(); }
            catch (Exception e) { errors.Add(e); }
            await actions;
            try { await ToggleManager.Input.ReleaseAllAsync(); }
            catch (Exception e) { errors.Add(e); }
            Try(() => _window?.FinishCleanup(), errors);
            Try(ToggleManager.Dispose, errors);
            foreach (var error in errors) Trace.TraceError(error.ToString());
            if (errors.Count > 0)
                MessageBox.Show(string.Join(Environment.NewLine, errors.ConvertAll(e => e.Message)),
                    "Tablet Friend cleanup", MessageBoxButton.OK, MessageBoxImage.Warning);
            ShutdownComplete = true;
            Application.Current.Shutdown();
        }

        private static void Try(Action action, List<Exception> errors)
        {
            try { action(); } catch (Exception e) { errors.Add(e); }
        }

        public static void ReportError(string message)
        {
            Trace.TraceError(message);
            if (IsShuttingDown) return;
            if (_window != null) _window.ReportError(message);
        }
    }
}
