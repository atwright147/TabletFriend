/////////////////////////////////////////////////////////////
/// All credit goes to https://github.com/beavis28/AppBar
/// You are my savior.
/////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WpfAppBar
{
	public enum DockingMode : int
	{
		Left = 0,
		Top,
		Right,
		Bottom,
		None
	}

	public static class AppBarFunctions
	{
		private sealed class RegisterInfo
		{
			internal Window Window;
			internal HwndSource Source;
			internal IntPtr Handle;
			internal int CallbackId;
			internal bool IsRegistered;
			internal DockingMode Edge;
			internal AppBarPositionUpdate PositionUpdate;

			internal IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
			{
				if (!IsRegistered) return IntPtr.Zero;
				if (msg == CallbackId && (wParam.ToInt32() == (int)Interop.ABNotify.ABN_POSCHANGED
					|| wParam.ToInt32() == (int)Interop.ABNotify.ABN_STATECHANGE))
				{
					PositionUpdate.Request();
					handled = true;
				}
				else if (msg == Interop.WM_SETTINGCHANGE || msg == Interop.WM_DISPLAYCHANGE || msg == Interop.WM_DPICHANGED)
				{
					// Taskbar/posture changes need not change resolution. Let WPF update its metrics first.
					PositionUpdate.Request();
				}
				else if (msg == Interop.WM_WINDOWPOSCHANGED)
				{
					var data = CreateBarData(this);
					Interop.SHAppBarMessage((int)Interop.ABMsg.ABM_WINDOWPOSCHANGED, ref data);
				}
				return IntPtr.Zero;
			}

			internal void OnClosed(object sender, EventArgs e) => SetAppBar(Window, DockingMode.None);
		}

		private static readonly Dictionary<Window, RegisterInfo> RegisteredWindowInfo = new Dictionary<Window, RegisterInfo>();

		public static void SetAppBar(Window appbarWindow, DockingMode edge, bool forceRedock = false)
		{
			RegisteredWindowInfo.TryGetValue(appbarWindow, out var info);
			if (edge == DockingMode.None)
			{
				if (info == null) return;
				info.IsRegistered = false;
				info.PositionUpdate.Cancel();
				if (!info.Source.IsDisposed) info.Source.RemoveHook(info.WndProc);
				appbarWindow.Closed -= info.OnClosed;
				RegisteredWindowInfo.Remove(appbarWindow);
				var data = CreateBarData(info);
				Interop.SHAppBarMessage((int)Interop.ABMsg.ABM_REMOVE, ref data);
				SetPeekPolicy(data.hWnd, false);
				return;
			}

			if (info != null && info.Edge == edge && !forceRedock) return;
			var handle = new WindowInteropHelper(appbarWindow).Handle;
			if (handle == IntPtr.Zero) return;

			if (info == null)
			{
				info = new RegisterInfo
				{
					Window = appbarWindow,
					Source = HwndSource.FromHwnd(handle),
					Handle = handle,
					CallbackId = Interop.RegisterWindowMessage("AppBarMessage"),
					Edge = edge
				};
				var registration = info;
				info.PositionUpdate = new AppBarPositionUpdate(
					action => appbarWindow.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, action),
					() => QueryPosition(registration),
					rect => DoResize(appbarWindow, rect));
				var data = CreateBarData(info);
				data.uCallbackMessage = info.CallbackId;
				info.Source.AddHook(info.WndProc);
				if (Interop.SHAppBarMessage((int)Interop.ABMsg.ABM_NEW, ref data) == UIntPtr.Zero)
				{
					info.Source.RemoveHook(info.WndProc);
					return;
				}
				info.IsRegistered = true;
				RegisteredWindowInfo.Add(appbarWindow, info);
				appbarWindow.Closed += info.OnClosed;
			}

			info.Edge = edge;
			info.PositionUpdate.Cancel();
			appbarWindow.WindowStyle = WindowStyle.None;
			appbarWindow.ResizeMode = ResizeMode.NoResize;
			appbarWindow.Topmost = true;
			SetPeekPolicy(handle, true);
			info.PositionUpdate.Request();
		}

		private static Interop.APPBARDATA CreateBarData(RegisterInfo info) => new Interop.APPBARDATA
		{
			cbSize = Marshal.SizeOf<Interop.APPBARDATA>(),
			hWnd = info.Handle,
			uEdge = (int)info.Edge
		};

		private static void SetPeekPolicy(IntPtr handle, bool docked)
		{
			var value = docked ? 1 : 0;
			Interop.DwmSetWindowAttribute(handle, (int)Interop.DWMWINDOWATTRIBUTE.DWMA_EXCLUDED_FROM_PEEK, ref value, sizeof(int));
			Interop.DwmSetWindowAttribute(handle, (int)Interop.DWMWINDOWATTRIBUTE.DWMA_DISALLOW_PEEK, ref value, sizeof(int));
		}

		private static void DoResize(Window window, Rect rect)
		{
			window.Width = rect.Width;
			window.Height = rect.Height;
			window.Top = rect.Top;
			window.Left = rect.Left;
		}

		private static Rect QueryPosition(RegisterInfo info)
		{
			var window = info.Window;
			var barData = CreateBarData(info);
			var toPixel = info.Source.CompositionTarget.TransformToDevice;
			var toWpfUnit = info.Source.CompositionTarget.TransformFromDevice;
			// ActualWidth/Height may still belong to the previous layout.
			var sizeInPixels = toPixel.Transform(new Vector(window.Width, window.Height));
			var vertical = info.Edge == DockingMode.Left || info.Edge == DockingMode.Right;
			var thickness = Math.Max(1, (int)Math.Round(vertical ? sizeInPixels.X : sizeInPixels.Y));
			// Always start with the full primary screen, not a previously reserved work area.
			barData.rc = AppBarGeometry.RequestedBounds(info.Edge,
				Interop.GetSystemMetrics(Interop.SM_CXSCREEN), Interop.GetSystemMetrics(Interop.SM_CYSCREEN), thickness);
			Interop.SHAppBarMessage((int)Interop.ABMsg.ABM_QUERYPOS, ref barData);
			barData.rc = AppBarGeometry.WithThickness(barData.rc, info.Edge, thickness);
			Interop.SHAppBarMessage((int)Interop.ABMsg.ABM_SETPOS, ref barData);

			// On Windows 11, app-bar negotiation can exclude the expanded tablet
			// taskbar even while the work area extends to its collapsed boundary.
			// Keep the shell reservation, but fill the actual usable perpendicular
			// span. Read it after SETPOS so it includes the current reservation.
			if (Interop.SystemParametersInfo(Interop.SPI_GETWORKAREA, 0, out var workArea, 0))
				barData.rc = AppBarGeometry.WithWorkAreaSpan(barData.rc, workArea, info.Edge);

			var location = toWpfUnit.Transform(new Point(barData.rc.left, barData.rc.top));
			var dimension = toWpfUnit.Transform(new Vector(barData.rc.right - barData.rc.left,
				barData.rc.bottom - barData.rc.top));
			return new Rect(location, new Size(dimension.X, dimension.Y));
		}
	}
}
