using System;
using System.Windows;

namespace WpfAppBar
{
    // All calls run on the window dispatcher. Query after pending WPF layout work,
    // then apply after the shell's own resize messages, unless a newer update won.
    internal sealed class AppBarPositionUpdate
    {
        private readonly Action<Action> _post;
        private readonly Func<Rect> _query;
        private readonly Action<Rect> _apply;
        private int _version;
        private bool _pending;
        private bool _querying;

        internal AppBarPositionUpdate(Action<Action> post, Func<Rect> query, Action<Rect> apply)
        {
            _post = post;
            _query = query;
            _apply = apply;
        }

        internal void Request()
        {
            // Reserving our own space can synchronously broadcast work-area changes.
            if (_pending || _querying) return;
            _pending = true;
            var version = ++_version;
            _post(() =>
            {
                if (version != _version) return;
                _pending = false;
                Rect bounds;
                _querying = true;
                try { bounds = _query(); }
                finally { _querying = false; }
                _post(() =>
                {
                    if (version == _version) _apply(bounds);
                });
            });
        }

        internal void Cancel()
        {
            ++_version;
            _pending = false;
        }
    }
}
