using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using WindowsInput.Events;
using Xunit;

namespace TabletFriend.Tests
{
    public class PressHoldButtonTests
    {
        [Fact]
        public void RenderedIconAndTemplateDoNotEnableHoldToRightClick()
        {
            Sta.Run(async () =>
            {
                var fixture = new Fixture();
                var icon = new TextBlock { Text = "Ctrl" };
                fixture.Button.Content = icon;
                fixture.Button.Template = (ControlTemplate)XamlReader.Parse(
                    "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ToggleButton'>" +
                    "<Border Background='Transparent'><ContentPresenter /></Border></ControlTemplate>");
                fixture.Button.ApplyTemplate();
                fixture.Button.Measure(new Size(100, 100));
                fixture.Button.Arrange(new Rect(0, 0, 100, 100));
                fixture.Button.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

                Assert.False(Stylus.GetIsPressAndHoldEnabled(icon));
                Assert.False(Stylus.GetIsFlicksEnabled(icon));
                AssertGesturesDisabled(fixture.Button);

                // Route a real preview event from the displayed content, not the button itself.
                var touch = new TestTouch(1);
                icon.RaiseEvent(new TouchEventArgs(touch, 0) { RoutedEvent = UIElement.PreviewTouchDownEvent });
                await fixture.Drain();
                Assert.True(fixture.Input.IsHeld(KeyCode.LControl));
                fixture.Now = TimeSpan.FromSeconds(1);
                icon.RaiseEvent(new TouchEventArgs(touch, 1000) { RoutedEvent = UIElement.PreviewTouchUpEvent });
                await fixture.Drain();
                Assert.False(fixture.Input.IsHeld(KeyCode.LControl));
                fixture.Button.Detach();
                await fixture.Drain();
            });
        }

        [Fact]
        public void RightClickDuringTouchHoldDoesNotReachToolbarOrReleaseModifier()
        {
            Sta.Run(async () =>
            {
                var fixture = new Fixture();
                var parent = new Border { Child = fixture.Button };
                var rightClicks = 0;
                parent.MouseRightButtonUp += (_, _) => rightClicks++;
                var touch = new TestTouch(1);
                fixture.Touch(touch, UIElement.PreviewTouchDownEvent);
                foreach (var routedEvent in new[] { UIElement.PreviewMouseRightButtonDownEvent, UIElement.PreviewMouseRightButtonUpEvent })
                {
                    var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = routedEvent };
                    fixture.Button.RaiseEvent(args);
                    Assert.True(args.Handled);
                }
                await fixture.Drain();
                Assert.Equal(0, rightClicks);
                Assert.True(fixture.Input.IsHeld(KeyCode.LControl));
                fixture.Now = TimeSpan.FromSeconds(1);
                fixture.Touch(touch, UIElement.PreviewTouchUpEvent);
                await fixture.Drain();
                Assert.False(fixture.Input.IsHeld(KeyCode.LControl));
                var mouseRightClick = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right)
                    { RoutedEvent = UIElement.PreviewMouseRightButtonUpEvent };
                fixture.Button.RaiseEvent(mouseRightClick);
                Assert.False(mouseRightClick.Handled);
                fixture.Button.Detach();
                await fixture.Drain();
            });
        }

        private static void AssertGesturesDisabled(DependencyObject element)
        {
            Assert.False(Stylus.GetIsPressAndHoldEnabled(element));
            Assert.False(Stylus.GetIsFlicksEnabled(element));
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                AssertGesturesDisabled(VisualTreeHelper.GetChild(element, i));
        }

        [Fact]
        public void MenuRequestsFromIconAreBlockedDuringAndAfterTouchButMouseAndKeyboardStillWork()
        {
            Sta.Run(async () =>
            {
                var fixture = new Fixture();
                var icon = new TextBlock { Text = "Ctrl" };
                fixture.Button.Content = icon;
                var toolbar = new Border { Child = fixture.Button, ContextMenu = new ContextMenu() };
                var requests = 0;
                toolbar.ContextMenuOpening += (_, _) => requests++;
                var touch = new TestTouch(1);
                fixture.Touch(touch, UIElement.PreviewTouchDownEvent);
                Assert.True(RequestContextMenu(icon).Handled);
                await fixture.Drain();
                Assert.True(fixture.Input.IsHeld(KeyCode.LControl));
                fixture.Touch(touch, UIElement.PreviewTouchUpEvent);
                Assert.True(RequestContextMenu(icon).Handled); // Delayed driver request after lifting.
                await fixture.Drain();
                Assert.True(fixture.Input.IsHeld(KeyCode.LControl)); // The tap lock survives.
                Assert.Equal(0, requests);

                fixture.Button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right)
                    { RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent });
                fixture.Button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right)
                    { RoutedEvent = UIElement.PreviewMouseRightButtonUpEvent });
                Assert.False(RequestContextMenu(icon).Handled);
                Assert.False(RequestContextMenu(icon, -1, -1).Handled);
                Assert.Equal(2, requests);
                fixture.Button.Detach();
                await fixture.Drain();
            });
        }

        private static ContextMenuEventArgs RequestContextMenu(FrameworkElement source, double x = 10, double y = 10)
        {
            // WPF creates this event through an internal constructor; exercise its actual
            // bubbling route without injecting desktop input or opening a real popup.
            var args = (ContextMenuEventArgs)Activator.CreateInstance(typeof(ContextMenuEventArgs),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { source, true, x, y }, null);
            source.RaiseEvent(args);
            return args;
        }

        [Fact]
        public void TouchEventsHoldLockHighlightAndIgnoreDuplicateMouseDown()
        {
            Sta.Run(async () =>
            {
                var fixture = new Fixture();
                var touch = new TestTouch(1);
                fixture.Touch(touch, UIElement.PreviewTouchDownEvent);
                fixture.Mouse(UIElement.PreviewMouseLeftButtonDownEvent);
                await fixture.Drain();
                Assert.True(fixture.Button.IsChecked);
                Assert.Single(fixture.Output.Events);
                fixture.Touch(touch, UIElement.PreviewTouchUpEvent);
                await fixture.Drain();
                Assert.True(fixture.Button.IsChecked);
                fixture.Button.RaiseEvent(new TouchEventArgs(touch, 0) { RoutedEvent = UIElement.LostTouchCaptureEvent });
                await fixture.Drain();
                Assert.True(fixture.Input.IsHeld(KeyCode.LControl));
                fixture.Touch(touch, UIElement.PreviewTouchDownEvent);
                fixture.Touch(touch, UIElement.PreviewTouchUpEvent);
                await fixture.Drain();
                Assert.False(fixture.Button.IsChecked);
                fixture.Button.Detach();
                await fixture.Drain();
            });
        }

        [Fact]
        public void MouseCaptureLossCancelsAndNormalClickDoesNotExecute()
        {
            Sta.Run(async () =>
            {
                var fixture = new Fixture();
                fixture.Mouse(UIElement.PreviewMouseLeftButtonDownEvent);
                await fixture.Drain();
                Assert.True(fixture.Button.IsChecked);
                fixture.Button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
                await fixture.Drain();
                Assert.False(fixture.Button.IsChecked);
                fixture.Mouse(UIElement.PreviewMouseLeftButtonUpEvent);
                await fixture.Drain();
                Assert.Equal(2, fixture.Output.Events.Count);
                fixture.Button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal(2, fixture.Output.Events.Count);
                fixture.Button.Detach();
                await fixture.Drain();
            });
        }

        [Theory]
        [InlineData("unload")]
        [InlineData("hide")]
        [InlineData("disable")]
        [InlineData("detach")]
        public void DisappearingControlsReleaseTapLocks(string reason)
        {
            Sta.Run(async () =>
            {
                var fixture = new Fixture();
                // Attach to a hidden native source so effective visibility can change without
                // showing a window, loading application settings, or injecting real input.
                using var source = new HwndSource(new HwndSourceParameters("PressHold visibility test")
                {
                    Width = 100, Height = 100, WindowStyle = unchecked((int)0x80000000)
                });
                source.RootVisual = fixture.Button;
                Assert.True(fixture.Button.IsVisible);
                var touch = new TestTouch(1);
                fixture.Touch(touch, UIElement.PreviewTouchDownEvent);
                fixture.Touch(touch, UIElement.PreviewTouchUpEvent);
                await fixture.Drain();
                Assert.True(fixture.Input.IsHeld(KeyCode.LControl));
                switch (reason)
                {
                    case "unload": fixture.Button.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); break;
                    case "hide": fixture.Button.Visibility = Visibility.Hidden; break;
                    case "disable": fixture.Button.IsEnabled = false; break;
                    case "detach": fixture.Button.Detach(); break;
                }
                await fixture.Drain();
                Assert.False(fixture.Input.IsHeld(KeyCode.LControl));
                fixture.Button.Detach();
                await fixture.Drain();
            });
        }

        [Fact]
        public void OutsideTouchReleaseAndWrongPointerUpCannotLockOrEndAnotherPress()
        {
            Sta.Run(async () =>
            {
                var fixture = new Fixture();
                var touch = new TestTouch(1);
                fixture.Touch(touch, UIElement.PreviewTouchDownEvent);
                fixture.Touch(new TestTouch(2), UIElement.PreviewTouchUpEvent);
                await fixture.Drain();
                Assert.True(fixture.Input.IsHeld(KeyCode.LControl));
                touch.Position = new Point(150, 150);
                fixture.Touch(touch, UIElement.PreviewTouchUpEvent);
                await fixture.Drain();
                Assert.False(fixture.Input.IsHeld(KeyCode.LControl));
                fixture.Button.Detach();
                await fixture.Drain();
            });
        }

        [Fact]
        public void CaptureFailureDoesNotInjectKeysAndPhysicalReleaseRefreshesHighlight()
        {
            Sta.Run(async () =>
            {
                var fixture = new Fixture();
                fixture.CanCapture = false;
                fixture.Mouse(UIElement.PreviewMouseLeftButtonDownEvent);
                await fixture.Drain();
                Assert.Empty(fixture.Output.Events);
                fixture.CanCapture = true;
                fixture.Touch(new TestTouch(1), UIElement.PreviewTouchDownEvent);
                await fixture.Drain();
                Assert.True(fixture.Button.IsChecked);
                fixture.Input.ObservePhysicalKeyUp(KeyCode.LControl);
                Assert.False(fixture.Button.IsChecked);
                fixture.Button.Detach();
                await fixture.Drain();
            });
        }

        private sealed class Fixture
        {
            public readonly HoldKeyboard Output = new HoldKeyboard();
            public readonly InputStateService Input;
            public readonly PressHoldButton Button;
            public bool CanCapture = true;
            public TimeSpan Now;
            private readonly List<Task> _tasks = new List<Task>();

            public Fixture()
            {
                Input = new InputStateService(Output);
                Button = new PressHoldButton(Input, new[] { KeyCode.LControl }, true, 250,
                    () => Now, action =>
                    {
                        var task = action();
                        _tasks.Add(task);
                        return task;
                    }, _ => CanCapture);
                Button.Measure(new Size(100, 100));
                Button.Arrange(new Rect(0, 0, 100, 100));
            }

            public void Touch(TestTouch touch, RoutedEvent routedEvent)
            {
                var args = new TouchEventArgs(touch, 0) { RoutedEvent = routedEvent };
                Button.RaiseEvent(args);
                Assert.True(args.Handled);
            }

            public void Mouse(RoutedEvent routedEvent) => Button.RaiseEvent(
                new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = routedEvent });

            public Task Drain() => Task.WhenAll(_tasks.ToArray());
        }

        private sealed class TestTouch : TouchDevice
        {
            public Point Position = new Point(10, 10);
            public TestTouch(int id) : base(id) { }
            public override TouchPoint GetTouchPoint(IInputElement relativeTo) =>
                new TouchPoint(this, Position, new Rect(Position, new Size(1, 1)), TouchAction.Move);
            public override TouchPointCollection GetIntermediateTouchPoints(IInputElement relativeTo) =>
                new TouchPointCollection { GetTouchPoint(relativeTo) };
        }
    }
}
