using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TabletFriend.Docking;
using WpfAppBar;
using Xunit;

namespace TabletFriend.Tests
{
    public class DockingTests
    {
        [Theory]
        [InlineData(DockingMode.Left, 0, 192)]
        [InlineData(DockingMode.Right, 2688, 2880)]
        public void CollapsedTabletTaskbarUsesWorkAreaDespiteExpandedShellReservation(DockingMode edge, int left, int right)
        {
            var queue = new Queue<Action>();
            var applied = new List<Rect>();
            var workBottom = 1872;
            var reserved = new Interop.RECT { left = left, right = right, bottom = 1776 };
            var update = new AppBarPositionUpdate(queue.Enqueue, () =>
            {
                // Live device: 2880x1920 at 200%; shell excludes 144px but the
                // collapsed taskbar only excludes 48px from the desktop work area.
                var workArea = new Interop.RECT
                {
                    left = edge == DockingMode.Left ? 192 : 0,
                    right = edge == DockingMode.Right ? 2688 : 2880,
                    bottom = workBottom
                };
                var bounds = AppBarGeometry.WithWorkAreaSpan(reserved, workArea, edge);
                return new Rect(bounds.left / 2.0, bounds.top / 2.0,
                    (bounds.right - bounds.left) / 2.0, (bounds.bottom - bounds.top) / 2.0);
            }, applied.Add);

            foreach (var bottom in new[] { 1872, 1824, 1776, 1872 })
            {
                workBottom = bottom;
                update.Request();
                Drain(queue);
                var actual = applied[applied.Count - 1];
                Assert.Equal(bottom / 2.0, actual.Bottom);
                Assert.Equal(left / 2.0, actual.Left);
                Assert.Equal(96, actual.Width);
            }
            // Window extension must not modify the shell's reservation.
            Assert.Equal(1776, reserved.bottom);
        }

        [Theory]
        [InlineData(DockingMode.Top, 0, 100)]
        [InlineData(DockingMode.Bottom, 1820, 1920)]
        public void HorizontalSpanExcludesSideBarsWithoutSubtractingItsOwnHeight(DockingMode edge, int top, int bottom)
        {
            var reserved = new Interop.RECT { top = top, bottom = bottom, right = 2880 };
            var workArea = new Interop.RECT { left = 192, right = 2800, top = 100, bottom = 1820 };
            var bounds = AppBarGeometry.WithWorkAreaSpan(reserved, workArea, edge);
            Assert.Equal(192, bounds.left);
            Assert.Equal(2800, bounds.right);
            Assert.Equal(top, bounds.top);
            Assert.Equal(bottom, bounds.bottom);
        }

        [Fact]
        public void VerticalSpanRespectsTopAndBottomBarsAndIgnoresInvalidWorkArea()
        {
            var reserved = new Interop.RECT { right = 192, bottom = 1776 };
            var workArea = new Interop.RECT { left = 192, right = 2880, top = 100, bottom = 1872 };
            var bounds = AppBarGeometry.WithWorkAreaSpan(reserved, workArea, DockingMode.Left);
            Assert.Equal(100, bounds.top);
            Assert.Equal(1872, bounds.bottom);
            Assert.Equal(0, bounds.left);
            Assert.Equal(192, bounds.right);
            Assert.Equal(reserved, AppBarGeometry.WithWorkAreaSpan(reserved, default, DockingMode.Left));
        }

        [Theory]
        [InlineData(DockingMode.Left)]
        [InlineData(DockingMode.Right)]
        public void TaskbarHeightChangesUseTheFullScreenForEveryQuery(DockingMode edge)
        {
            var queue = new Queue<Action>();
            var taskbarHeight = 144;
            var applied = new List<Rect>();
            var queries = 0;
            var update = new AppBarPositionUpdate(queue.Enqueue, () =>
            {
                ++queries;
                var bounds = AppBarGeometry.RequestedBounds(edge, 2880, 1920, 192);
                Assert.Equal(1920, bounds.bottom);
                // Simulate the shell reserving the current taskbar height.
                bounds.bottom -= taskbarHeight;
                bounds = AppBarGeometry.WithThickness(bounds, edge, 192);
                return new Rect(bounds.left, bounds.top, bounds.right - bounds.left, bounds.bottom - bounds.top);
            }, applied.Add);

            update.Request();
            update.Request();
            taskbarHeight = 48; // Change again before the queued query runs.
            Drain(queue);
            Assert.Equal(1, queries);
            Assert.Equal(1872, Assert.Single(applied).Height);

            foreach (var height in new[] { 144, 48, 0, 144 })
            {
                taskbarHeight = height;
                update.Request();
                Drain(queue);
                Assert.Equal(1920 - height, applied[applied.Count - 1].Height);
                Assert.Equal(192, applied[applied.Count - 1].Width);
            }
        }

        [Theory]
        [InlineData(DockingMode.Left, 40, 20, 140, 900)]
        [InlineData(DockingMode.Right, 1760, 20, 1860, 900)]
        [InlineData(DockingMode.Top, 40, 20, 1860, 120)]
        [InlineData(DockingMode.Bottom, 40, 800, 1860, 900)]
        public void ShellAdjustmentPreservesThicknessAndTheOtherAxis(DockingMode edge, int left, int top, int right, int bottom)
        {
            var queried = new Interop.RECT { left = 40, top = 20, right = 1860, bottom = 900 };
            var bounds = AppBarGeometry.WithThickness(queried, edge, 100);
            Assert.Equal(left, bounds.left);
            Assert.Equal(top, bounds.top);
            Assert.Equal(right, bounds.right);
            Assert.Equal(bottom, bounds.bottom);
        }

        [Fact]
        public void NewNotificationDiscardsAnAlreadyCalculatedResize()
        {
            var queue = new Queue<Action>();
            var bounds = new Rect(0, 0, 100, 900);
            var applied = new List<Rect>();
            var update = new AppBarPositionUpdate(queue.Enqueue, () => bounds, applied.Add);
            update.Request();
            queue.Dequeue()(); // The old query has run, but its resize has not.
            bounds = new Rect(0, 0, 100, 1000);
            update.Request();
            Drain(queue);
            Assert.Equal(bounds, Assert.Single(applied));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UndockingCancelsQueuedQueriesAndResizes(bool queryAlreadyRan)
        {
            var queue = new Queue<Action>();
            var applied = new List<Rect>();
            var queries = 0;
            var update = new AppBarPositionUpdate(queue.Enqueue, () =>
            {
                ++queries;
                return new Rect(0, 0, 100, 900);
            }, applied.Add);
            update.Request();
            if (queryAlreadyRan) queue.Dequeue()();
            update.Cancel();
            Drain(queue);
            Assert.Empty(applied);
            Assert.Equal(queryAlreadyRan ? 1 : 0, queries);
            update.Request();
            Drain(queue);
            Assert.Single(applied);
        }

        [Fact]
        public void ReservingSpaceDoesNotRecursivelyRespondToItsOwnNotification()
        {
            var queue = new Queue<Action>();
            var applied = new List<Rect>();
            AppBarPositionUpdate update = null;
            update = new AppBarPositionUpdate(queue.Enqueue, () =>
            {
                update.Request();
                return new Rect(0, 0, 100, 900);
            }, applied.Add);
            update.Request();
            Drain(queue);
            Assert.Single(applied);
        }

        [Theory]
        [InlineData(DockingMode.Left)]
        [InlineData(DockingMode.Right)]
        [InlineData(DockingMode.Top)]
        [InlineData(DockingMode.Bottom)]
        public void ContentsFollowTheAllocatedAreaWithoutRecreatingControls(DockingMode edge)
        {
            Sta.Run(() =>
            {
                var vertical = edge == DockingMode.Left || edge == DockingMode.Right;
                var canvas = new ToolbarCanvas();
                var button = new Button();
                canvas.Children.Add(button);
                canvas.SetLayout(edge, vertical ? new Size(100, 800) : new Size(800, 100));
                foreach (var length in new[] { 900, 1000, 850, 700 })
                {
                    var size = vertical ? new Size(100, length) : new Size(length, 100);
                    canvas.Measure(size);
                    canvas.Arrange(new Rect(size));
                    canvas.UpdateLayout();
                    var offset = (TranslateTransform)canvas.RenderTransform;
                    Assert.Equal(Math.Max(0, (length - 800) / 2), vertical ? offset.Y : offset.X);
                    Assert.Equal(0, vertical ? offset.X : offset.Y);
                    Assert.Same(button, Assert.Single(canvas.Children));
                }
                canvas.SetLayout(DockingMode.None, new Size(100, 800));
                var undocked = (TranslateTransform)canvas.RenderTransform;
                Assert.Equal(0, undocked.X);
                Assert.Equal(0, undocked.Y);
            });
        }

        private static void Drain(Queue<Action> queue)
        {
            var remaining = 20;
            while (queue.Count > 0)
            {
                Assert.True(--remaining > 0, "Docking updates did not settle.");
                queue.Dequeue()();
            }
        }
    }
}
