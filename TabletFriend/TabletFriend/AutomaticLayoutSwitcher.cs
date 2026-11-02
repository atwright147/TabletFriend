using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;

namespace TabletFriend
{
	public class AutomaticLayoutSwitcher : IDisposable
	{
		private bool _disposed;
		private AppFocusMonitor _monitor;

		private Dictionary<string, string> _appSpecificLayouts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public AutomaticLayoutSwitcher(AppFocusMonitor monitor)
		{
			_monitor = monitor;
			_monitor.OnAppChanged += OnAppChanged;

			OnUpdateLayoutList();
			EventBeacon.Subscribe(Events.UpdateLayoutList, OnUpdateLayoutList);
		}


		private void OnUpdateLayoutList(params object[] obj)
		{
			_appSpecificLayouts.Clear();

			foreach (var layout in AppState.Layouts)
			{
				if (string.IsNullOrEmpty(layout.Value.App))
				{
					continue;
				}
				var appRegex = WildCardToRegular(layout.Value.App);
				if (!_appSpecificLayouts.ContainsKey(appRegex))
				{
					_appSpecificLayouts.Add(appRegex, layout.Key);
				}
			}
		}


		private void OnAppChanged(string app)
		{
			if (_disposed || !AppState.Settings.PerAppLayoutsEnabled)
			{
				return;
			}

			if (Application.Current == null)
			{ 
				// Yep. Can happen.
				return;
			}
			Application.Current.Dispatcher.BeginInvoke(new Action(
				delegate
				{
					if (_disposed || AppLifecycle.IsShuttingDown) return;
					if (MatchesApp(app, out var key))
					{
						EventBeacon.SendEvent(Events.ChangeLayout, key, LayoutChangeMethod.Automatic);
					}
					else
					{
						if (AppState.LastManuallySetLayout != null)
						{
							EventBeacon.SendEvent(Events.ChangeLayout, AppState.LastManuallySetLayout, LayoutChangeMethod.Automatic);
						}
					}
				}
			));
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			_monitor.OnAppChanged -= OnAppChanged;
			EventBeacon.Unsubscribe(Events.UpdateLayoutList, OnUpdateLayoutList);
		}

		private bool MatchesApp(string app, out string layout)
		{
			foreach(var regex in _appSpecificLayouts)
			{
				if (Regex.IsMatch(app, regex.Key, RegexOptions.IgnoreCase))
				{
					layout = regex.Value;
					return true;
				}
			}
			layout = null;
			return false;
		}

		private static string WildCardToRegular(string value) =>
			 "^" + Regex.Escape(value).Replace("\\*", ".*") + "$";
	}
}
