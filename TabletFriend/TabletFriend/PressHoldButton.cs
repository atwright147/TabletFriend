using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WindowsInput.Events;

namespace TabletFriend
{
    internal sealed class PressHoldButton : ToggleButton
    {
        private readonly InputStateService _input;
        private readonly PressHoldController _controller;
        private readonly Func<Func<Task>, Task> _run;
        private readonly Func<InputDevice, bool> _capture;
        private InputDevice _pointer;
        private bool _subscribed;
        private bool _detached;
        private bool _mouseContextMenuRequested;

        internal PressHoldButton(InputStateService input, KeyCode[] keys, bool quickToggle, int thresholdMs,
            Func<TimeSpan> now = null, Func<Func<Task>, Task> run = null,
            Func<InputDevice, bool> capture = null)
        {
            _input = input;
            _controller = new PressHoldController(input, keys, quickToggle, thresholdMs, now);
            _run = run ?? ActionRunner.RunAsync;
            _capture = capture ?? CapturePointer;
            Stylus.SetIsPressAndHoldEnabled(this, false);
            Stylus.SetIsFlicksEnabled(this, false);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += (_, _) => { if (!IsVisible) Cancel(); };
            IsEnabledChanged += (_, _) => { if (!IsEnabled) Cancel(); };
            Subscribe();
        }

        // State is driven by keyboard ownership, never by ToggleButton's default click logic.
        protected override void OnToggle() { }
        protected override void OnClick() { }

        protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
        {
            _mouseContextMenuRequested = _pointer == null && e.StylusDevice == null;
            if (!_mouseContextMenuRequested) e.Handled = true;
            else base.OnPreviewMouseRightButtonDown(e);
        }

        protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e)
        {
            if (_pointer != null || e.StylusDevice != null)
            {
                _mouseContextMenuRequested = false;
                e.Handled = true;
            }
            else base.OnPreviewMouseRightButtonUp(e);
        }

        protected override void OnContextMenuOpening(ContextMenuEventArgs e)
        {
            // A touch driver can request a menu directly, even after touch-up. Only
            // intentional mouse/keyboard requests may bubble to the window's menu.
            var keyboardRequest = e.CursorLeft == -1 && e.CursorTop == -1;
            if (_pointer != null || (!keyboardRequest && !_mouseContextMenuRequested))
                e.Handled = true;
            _mouseContextMenuRequested = false;
            base.OnContextMenuOpening(e);
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (e.StylusDevice == null) Begin(e.MouseDevice);
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (e.StylusDevice == null) End(e.MouseDevice, e.GetPosition(this));
        }

        protected override void OnPreviewTouchDown(TouchEventArgs e)
        {
            e.Handled = true;
            Begin(e.TouchDevice);
        }

        protected override void OnPreviewTouchUp(TouchEventArgs e)
        {
            e.Handled = true;
            End(e.TouchDevice, e.GetTouchPoint(this).Position);
        }

        protected override void OnPreviewStylusDown(StylusDownEventArgs e)
        {
            // Touch has its own per-contact capture; let WPF promote it to touch events.
            if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch) return;
            e.Handled = true;
            Begin(e.StylusDevice);
        }

        protected override void OnPreviewStylusUp(StylusEventArgs e)
        {
            if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch) return;
            e.Handled = true;
            End(e.StylusDevice, e.GetPosition(this));
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (ReferenceEquals(_pointer, e.MouseDevice)) Cancel();
        }

        protected override void OnLostTouchCapture(TouchEventArgs e)
        {
            base.OnLostTouchCapture(e);
            if (ReferenceEquals(_pointer, e.TouchDevice)) Cancel();
        }

        protected override void OnLostStylusCapture(StylusEventArgs e)
        {
            base.OnLostStylusCapture(e);
            if (ReferenceEquals(_pointer, e.StylusDevice)) Cancel();
        }

        private bool CapturePointer(InputDevice pointer)
        {
            if (pointer is TouchDevice touch) return CaptureTouch(touch);
            if (pointer is StylusDevice stylus) return stylus.Capture(this);
            return CaptureMouse();
        }

        private void Begin(InputDevice pointer)
        {
            if (_detached || _pointer != null || !IsEnabled) return;
            _mouseContextMenuRequested = false;
            _pointer = pointer;
            if (!_capture(pointer)) { _pointer = null; return; }
            if (!ReferenceEquals(_pointer, pointer)) return;
            _ = _run(_controller.BeginAsync);
        }

        private void End(InputDevice pointer, Point position)
        {
            if (!ReferenceEquals(pointer, _pointer)) return;
            _ = _run(() => _controller.EndAsync(new Rect(RenderSize).Contains(position)));
            ReleasePointer();
        }

        private void ReleasePointer()
        {
            var pointer = _pointer;
            _pointer = null; // Expected capture-loss events must not cancel a successful tap lock.
            if (pointer is TouchDevice touch && touch.Captured == this) touch.Capture(null);
            else if (pointer is StylusDevice stylus && stylus.Captured == this) stylus.Capture(null);
            else if (pointer is MouseDevice mouse && mouse.Captured == this) ReleaseMouseCapture();
        }

        internal void Cancel()
        {
            ReleasePointer();
            _ = _run(_controller.CancelAsync);
        }

        internal void Detach()
        {
            _detached = true;
            Unsubscribe();
            Cancel();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_detached) Subscribe();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unsubscribe();
            Cancel();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _input.Changed += OnInputChanged;
            _subscribed = true;
            IsChecked = _controller.IsActive;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _input.Changed -= OnInputChanged;
            _subscribed = false;
        }

        private void OnInputChanged(KeyCode key, bool held)
        {
            if (Dispatcher.HasShutdownStarted) return;
            if (Dispatcher.CheckAccess()) Refresh();
            else Dispatcher.BeginInvoke(new Action(Refresh));
        }

        private void Refresh()
        {
            if (_subscribed) IsChecked = _controller.IsActive;
        }
    }
}
