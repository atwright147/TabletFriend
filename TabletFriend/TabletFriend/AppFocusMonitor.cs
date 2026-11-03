using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		internal Task Completion => _completion.Task;
		public HashSet<string> IgnoredApps = new HashSet<string>
		{
			"TabletFriend",
			"explorer" // Explorer takes over when you click the taskbar. 
		};

		public string FocusedApp { get; private set; }

		public event Action<string> OnAppChanged;

		public AppFocusMonitor() : this(Automation.AddAutomationFocusChangedEventHandler,
			Automation.RemoveAutomationFocusChangedEventHandler)
		{
		}

		internal AppFocusMonitor(Action<AutomationFocusChangedEventHandler> subscribe,
			Action<AutomationFocusChangedEventHandler> unsubscribe)
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
					{
						if (!IgnoredApps.Contains(process.ProcessName) && FocusedApp != process.ProcessName)
						{
							FocusedApp = process.ProcessName;
							OnAppChanged?.Invoke(FocusedApp);
						}
					}
				}
			}
			catch
			{ }
		}


		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			_stop.TrySetResult(true);
		}
	}
}
