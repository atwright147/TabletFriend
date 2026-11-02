using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TabletFriend.Data;
using TabletFriend.Models;

namespace TabletFriend
{
    public sealed class FileManager : IDisposable
    {
        private sealed class Snapshot
        {
            public DirectoryImport<LayoutData> Layouts;
            public DirectoryImport<ThemeData> Themes;
        }

        private readonly ConfigurationCache<LayoutData, LayoutModel> _layouts = new ConfigurationCache<LayoutData, LayoutModel>();
        private readonly ConfigurationCache<ThemeData, ThemeModel> _themes = new ConfigurationCache<ThemeData, ThemeModel>();
        private readonly ConcurrentDictionary<string, byte> _changed = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _lastErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _root;
        private readonly Dispatcher _dispatcher;
        private readonly ReloadCoordinator<Snapshot> _reload;
        private FileSystemWatcher _watcher;
        private volatile bool _disposed;
        private string _pendingErrors;
        public event Action<string> Errors;
        internal Task Completion => _reload.Completion;

        public FileManager() : this(AppState.FilesRoot, Application.Current.Dispatcher) { }

        internal FileManager(string root, Dispatcher dispatcher)
        {
            _root = root;
            _dispatcher = dispatcher;
            _reload = new ReloadCoordinator<Snapshot>(ReadAsync,
                action => _dispatcher.InvokeAsync(action).Task, snapshot => Publish(snapshot, true),
                error => QueueErrors(error.Message));

            // Preserve synchronous startup, but never construct a WPF object on a worker.
            var initial = Task.Run(() => ReadAsync(CancellationToken.None)).GetAwaiter().GetResult();
            Publish(initial, false);
            try
            {
                _watcher = new FileSystemWatcher(root)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                        NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = true
                };
                _watcher.Changed += OnChanged;
                _watcher.Created += OnChanged;
                _watcher.Deleted += OnChanged;
                _watcher.Renamed += OnRenamed;
                _watcher.Error += OnWatcherError;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            { QueueErrors($"Cannot watch files: {e.Message}"); }
        }

        private async Task<Snapshot> ReadAsync(CancellationToken token)
        {
            var layouts = await Importer.ReadDirectoryAsync<LayoutData>(Path.Combine(_root, "layouts"), token).ConfigureAwait(false);
            var themes = await Importer.ReadDirectoryAsync<ThemeData>(Path.Combine(_root, "themes"), token).ConfigureAwait(false);
            return new Snapshot { Layouts = layouts, Themes = themes };
        }

        private void OnChanged(object sender, FileSystemEventArgs e) => NotifyChanged(e.FullPath);
        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            NotifyChanged(e.OldFullPath);
            NotifyChanged(e.FullPath);
        }
        private void OnWatcherError(object sender, ErrorEventArgs e) => _reload.Notify();

        internal void NotifyChanged(string path)
        {
            if (_disposed) return;
            _changed[path] = 0;
            _reload.Notify();
        }

        private void Publish(Snapshot snapshot, bool notify)
        {
            if (_disposed) return;
            var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _layouts.Apply(snapshot.Layouts, data => new LayoutModel(data), errors);
            _themes.Apply(snapshot.Themes, data => new ThemeModel(data), errors);
            AppState.Layouts = _layouts.ByName();
            AppState.Themes = _themes.ByName();

            foreach (var path in _changed.Keys)
                if (_changed.TryRemove(path, out _)) _lastErrors.Remove(path);
            var newErrors = errors.Where(e => !_lastErrors.TryGetValue(e.Key, out var old) || old != e.Value)
                .Select(e => $"{Path.GetFileName(e.Key)}: {e.Value}").ToArray();
            _lastErrors = errors;

            if (notify)
            {
                // Reconcile selections before exposing rebuilt menus/app mappings.
                AppState.CurrentLayoutName = ConfigurationSelection.Select(AppState.Layouts, AppState.CurrentLayoutName);
                AppState.CurrentThemeName = ConfigurationSelection.Select(AppState.Themes, AppState.CurrentThemeName);
                AppState.LastManuallySetLayout = ConfigurationSelection.Select(AppState.Layouts, AppState.LastManuallySetLayout);
                EventBeacon.SendEvent(Events.UpdateThemeList);
                EventBeacon.SendEvent(Events.UpdateLayoutList);
                EventBeacon.SendEvent(Events.FilesChanged);
            }
            if (newErrors.Length > 0) QueueErrors(string.Join(Environment.NewLine, newErrors));
        }

        private void QueueErrors(string errors)
        {
            _pendingErrors = _pendingErrors == null ? errors : _pendingErrors + Environment.NewLine + errors;
            ReportPendingErrors();
        }

        public void ReportPendingErrors()
        {
            if (_disposed || _pendingErrors == null || Errors == null) return;
            var message = _pendingErrors;
            _pendingErrors = null;
            Errors.Invoke(message);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _reload.Dispose();
            _watcher?.Dispose();
            Errors = null;
        }
    }

    internal static class ConfigurationSelection
    {
        public static string Select<T>(Dictionary<string, T> items, string requested)
        {
            if (requested != null && items.ContainsKey(requested)) return requested;
            if (items.ContainsKey("default")) return "default";
            return items.Keys.OrderBy(key => key, StringComparer.Ordinal).FirstOrDefault() ?? "default";
        }
    }
}
