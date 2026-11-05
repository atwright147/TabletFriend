namespace WpfAppBar
{
    internal static class AppBarGeometry
    {
        internal static Interop.RECT WithWorkAreaSpan(Interop.RECT reserved, Interop.RECT workArea, DockingMode edge)
        {
            if (workArea.right <= workArea.left || workArea.bottom <= workArea.top) return reserved;
            // Our reservation only reduces the work area on the docking axis. Use
            // the perpendicular span so our own thickness is never subtracted twice.
            switch (edge)
            {
                case DockingMode.Left:
                case DockingMode.Right:
                    reserved.top = workArea.top;
                    reserved.bottom = workArea.bottom;
                    break;
                case DockingMode.Top:
                case DockingMode.Bottom:
                    reserved.left = workArea.left;
                    reserved.right = workArea.right;
                    break;
            }
            return reserved;
        }

        internal static Interop.RECT RequestedBounds(DockingMode edge, int screenWidth, int screenHeight, int thickness)
        {
            var bounds = new Interop.RECT { right = screenWidth, bottom = screenHeight };
            return WithThickness(bounds, edge, thickness);
        }

        // QUERYPOS subtracts occupied space without preserving the requested thickness.
        internal static Interop.RECT WithThickness(Interop.RECT bounds, DockingMode edge, int thickness)
        {
            switch (edge)
            {
                case DockingMode.Left: bounds.right = bounds.left + thickness; break;
                case DockingMode.Right: bounds.left = bounds.right - thickness; break;
                case DockingMode.Top: bounds.bottom = bounds.top + thickness; break;
                case DockingMode.Bottom: bounds.top = bounds.bottom - thickness; break;
            }
            return bounds;
        }
    }
}
