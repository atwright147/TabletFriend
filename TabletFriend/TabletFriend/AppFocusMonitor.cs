using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		public HashSet<string> IgnoredApps = new HashSet<string>
		{
			"TabletFriend",
			"explorer" // Explorer takes over when you click the taskbar. 
		};

		public string FocusedApp { get; private set; }

		public event Action<string> OnAppChanged;

		public AppFocusMonitor()
		{
			Automation.AddAutomationFocusChangedEventHandler(OnFocusChanged);
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
			Automation.RemoveAutomationFocusChangedEventHandler(OnFocusChanged);
		}
	}
}
