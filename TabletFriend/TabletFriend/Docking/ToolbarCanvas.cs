using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfAppBar;

namespace TabletFriend.Docking
{
    public sealed class ToolbarCanvas : Canvas
    {
        private DockingMode _edge = DockingMode.None;
        private Size _contentSize;
        private readonly TranslateTransform _offset = new TranslateTransform();

        public ToolbarCanvas() => RenderTransform = _offset;

        internal void SetLayout(DockingMode edge, Size contentSize)
        {
            _edge = edge;
            _contentSize = contentSize;
            UpdateOffset();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateOffset();
        }

        private void UpdateOffset()
        {
            // Reposition existing controls without rebuilding them or interrupting held keys.
            _offset.X = _edge == DockingMode.Top || _edge == DockingMode.Bottom
                ? Math.Max(0, (ActualWidth - _contentSize.Width) / 2) : 0;
            _offset.Y = _edge == DockingMode.Left || _edge == DockingMode.Right
                ? Math.Max(0, (ActualHeight - _contentSize.Height) / 2) : 0;
        }
    }
}
