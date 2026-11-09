using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace TabletFriend
{
	public interface IAppFocusMonitor
	{
		event Action<string> OnAppChanged;
		string FocusedApp { get; }
	}

	public class AppFocusMonitor : IDisposable, IAppFocusMonitor
	{
		private volatile bool _disposed;
		private readonly TaskCompletionSource<bool> _stop = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly Task _foregroundCompletion;
		private readonly object _focusLock = new object();
		internal Task Completion => Task.WhenAll(_completion.Task, _foregroundCompletion);
		public HashSet<string> IgnoredApps = new HashSet<string>
		{
			"TabletFriend",
			"explorer" // Explorer takes over when you click the taskbar. 
		};

		public string FocusedApp { get; private set; }

		public event Action<string> OnAppChanged;

		public AppFocusMonitor() : this(Automation.AddAutomationFocusChangedEventHandler,
			Automation.RemoveAutomationFocusChangedEventHandler, GetForegroundApp, TimeSpan.FromMilliseconds(200))
		{
		}

		internal AppFocusMonitor(Action<AutomationFocusChangedEventHandler> subscribe,
			Action<AutomationFocusChangedEventHandler> unsubscribe)
			: this(subscribe, unsubscribe, null, TimeSpan.Zero)
		{
		}

		internal AppFocusMonitor(Action<AutomationFocusChangedEventHandler> subscribe,
			Action<AutomationFocusChangedEventHandler> unsubscribe, Func<string> foregroundApp, TimeSpan pollInterval)
		{
			// UI Automation may message our own windows. Add/remove on the same MTA
			// thread, leaving the UI dispatcher free to service those messages.
			var thread = new Thread(() =>
			{
				try
				{
					subscribe(OnFocusChanged);
					try { _stop.Task.GetAwaiter().GetResult(); }
					finally { unsubscribe(OnFocusChanged); }
					_completion.TrySetResult(true);
				}
				catch (Exception error) { _completion.TrySetException(error); }
			}) { IsBackground = true, Name = "Tablet Friend focus monitor" };
			thread.SetApartmentState(ApartmentState.MTA);
			thread.Start();
			_foregroundCompletion = foregroundApp == null ? Task.CompletedTask :
				Task.Run(() => PollForegroundAsync(foregroundApp, pollInterval));
		}

		private async Task PollForegroundAsync(Func<string> foregroundApp, TimeSpan interval)
		{
			while (!_disposed)
			{
				string app = null;
				try { app = foregroundApp(); }
				catch { } // A window may close between reading its handle and process.
				ObserveApp(app);
				await Task.WhenAny(Task.Delay(interval), _stop.Task).ConfigureAwait(false);
			}
		}


		private void OnFocusChanged(object sender, AutomationFocusChangedEventArgs e)
		{
			if (_disposed) return;
			try
			{
				var focusedElement = (AutomationElement)sender;
				if (focusedElement != null)
				{
					using (var process = Process.GetProcessById(focusedElement.Current.ProcessId))
						ObserveApp(process.ProcessName);
				}
			}
			catch { }
		}

		private void ObserveApp(string app)
		{
			if (_disposed || string.IsNullOrEmpty(app) || IgnoredApps.Contains(app)) return;
			lock (_focusLock)
			{
				if (_disposed || string.Equals(FocusedApp, app, StringComparison.OrdinalIgnoreCase)) return;
				FocusedApp = app;
			}
			OnAppChanged?.Invoke(app);
		}

		private static string GetForegroundApp()
		{
			var window = GetForegroundWindow();
			if (window == IntPtr.Zero) return null;
			GetWindowThreadProcessId(window, out var processId);
			if (processId == 0) return null;
			using var process = Process.GetProcessById((int)processId);
			return process.ProcessName;
		}

		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);


		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			_stop.TrySetResult(true);
		}
	}
}
