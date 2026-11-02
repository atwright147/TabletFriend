using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TabletFriend.Docking;
using TabletFriend.TabletMode;
using WpfAppBar;

namespace TabletFriend
{
	public class TrayManager : IDisposable
	{
		private TaskbarIcon _icon;
		private MenuItem _autostartMenuItem;
		private MenuItem _autoUpdateMenuItem;
		private MenuItem _autohideMenuItem;
		private MenuItem _perAppLayoutsMenuItem;
		private readonly string _iconPathBlack = AppState.CurrentDirectory + "/files/icons/tray/tray_black.ico";
		private readonly string _iconPathWhite = AppState.CurrentDirectory + "/files/icons/tray/tray_white.ico";
		private readonly Func<MenuItem> _layoutMenu;
		private readonly Func<MenuItem> _themeMenu;
		private readonly ContextMenu _menu = new ContextMenu();
		private bool _disposed;

		private IAppFocusMonitor _focusMonitor;
		private MenuItem _focusedApp;
		public TrayManager(MainWindow window, LayoutListManager layoutList, ThemeListManager themeList, AppFocusMonitor focusMonitor)
			: this(layoutList.CloneMenu, themeList.CloneMenu, focusMonitor, true)
		{
		}

		internal TrayManager(Func<MenuItem> layoutMenu, Func<MenuItem> themeMenu, IAppFocusMonitor focusMonitor, bool createIcon = false)
		{
			_layoutMenu = layoutMenu;
			_themeMenu = themeMenu;
			_focusMonitor = focusMonitor;

			if (createIcon)
			{
				_icon = new TaskbarIcon();
				_icon.Icon = new Icon(_isLightTheme ? _iconPathBlack : _iconPathWhite);
				_icon.Visibility = Visibility.Visible;
				_icon.ContextMenu = _menu;
				_icon.TrayLeftMouseDown += MouseDown;
			}
			CreateMenu();
			_focusMonitor.OnAppChanged += OnAppChanged;

			EventBeacon.Subscribe(Events.ChangeLayout, OnUpdateLayoutList);
			EventBeacon.Subscribe(Events.FilesChanged, OnUpdateLayoutList);
		}

		private void MouseDown(object sender, RoutedEventArgs e)
		{
			EventBeacon.SendEvent(Events.ToggleMinimize);
		}

		private void OnUpdateLayoutList(object[] obj = null)
		{
			if (!_disposed) _menu.Dispatcher.Invoke(RebuildMenu);
		}

		internal void RebuildMenu()
		{
			if (_disposed) return;
			_menu.Items.Clear();
			CreateMenu();
		}

		internal MenuItem FocusedAppItem => _focusedApp;

		public void ShowError(string message)
		{
			if (!_disposed) _icon?.ShowBalloonTip("Tablet Friend", message, BalloonIcon.Warning);
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			_focusMonitor.OnAppChanged -= OnAppChanged;
			EventBeacon.Unsubscribe(Events.ChangeLayout, OnUpdateLayoutList);
			EventBeacon.Unsubscribe(Events.FilesChanged, OnUpdateLayoutList);
			if (_icon != null)
			{
				_icon.TrayLeftMouseDown -= MouseDown;
				_icon.Dispose();
			}
		}

		private void CreateMenu()
		{
			
			_menu.Items.Add(_layoutMenu());
			_menu.Items.Add(_themeMenu());

			DockingMenuFactory.CreateDockingMenu(_menu);

			var settings = new MenuItem() { Header = "settings" };

			if (AppState.Settings.AddToAutostart)
			{
				_autostartMenuItem = AddSubmenuItem(settings, "remove from autostart", OnAutostartToggle);
			}
			else
			{
				_autostartMenuItem = AddSubmenuItem(settings, "add to autostart", OnAutostartToggle);
			}

			if (AppState.Settings.UpdateCheckingEnabled)
			{
				_autoUpdateMenuItem = AddSubmenuItem(settings, "don't check for updates", OnAutoUpdateToggle);
			}
			else
			{
				_autoUpdateMenuItem = AddSubmenuItem(settings, "check for updates", OnAutoUpdateToggle);
			}

			if (AppState.Settings.ToolbarAutohideEnabled)
			{
				_autohideMenuItem = AddSubmenuItem(settings, "disable toolbar autohide", OnAutohideToggle);
			}
			else
			{
				_autohideMenuItem = AddSubmenuItem(settings, "enable toolbar autohide", OnAutohideToggle);
			}

			if (AppState.Settings.PerAppLayoutsEnabled)
			{
				_perAppLayoutsMenuItem = AddSubmenuItem(settings, "disable per-app layouts", OnPerAppLayoutsToggle);
			}
			else
			{
				_perAppLayoutsMenuItem = AddSubmenuItem(settings, "enable per-app layouts", OnPerAppLayoutsToggle);
			}


			AddSubmenuItem(settings, "open files directory...", OnOpenLayoutsDirectory);
			_focusedApp = AddSubmenuItem(settings, "focused app: " + (_focusMonitor.FocusedApp ?? "none"));
			_menu.Items.Add(settings);

			_menu.Items.Add(new Separator());
			AddMenuItem("about", OnAbout);
			AddMenuItem("quit", OnQuit);
		}

		private void OnAppChanged(string app)
		{
			if (_disposed) return;
			Action update = () => { if (!_disposed) _focusedApp.Header = "focused app: " + app; };
			if (_menu.Dispatcher.CheckAccess()) update();
			else if (!_menu.Dispatcher.HasShutdownStarted) _menu.Dispatcher.BeginInvoke(update);
		}

		private void OnAutostartToggle(object sender, RoutedEventArgs e)
		{
			AppState.Settings.AddToAutostart = !AppState.Settings.AddToAutostart;

			if (AppState.Settings.AddToAutostart)
			{
				AutostartManager.SetAutostart();
				_autostartMenuItem.Header = "remove from autostart";
			}
			else
			{
				AutostartManager.ResetAutostart();
				_autostartMenuItem.Header = "add to autostart";
			}
			EventBeacon.SendEvent(Events.UpdateSettings);
		}

		private void OnAutoUpdateToggle(object sender, RoutedEventArgs e)
		{
			AppState.Settings.UpdateCheckingEnabled = !AppState.Settings.UpdateCheckingEnabled;

			if (AppState.Settings.UpdateCheckingEnabled)
			{
				_autoUpdateMenuItem.Header = "don't check for updates";
			}
			else
			{
				_autoUpdateMenuItem.Header = "check for updates";
			}
			EventBeacon.SendEvent(Events.UpdateSettings);
		}

		private void OnAutohideToggle(object sender, RoutedEventArgs e)
		{
			AppState.Settings.ToolbarAutohideEnabled = !AppState.Settings.ToolbarAutohideEnabled;

			if (AppState.Settings.ToolbarAutohideEnabled)
			{
				_autohideMenuItem.Header = "disable toolbar autohide";
				ToolbarAutohider.StartWatching();
			}
			else
			{
				_autohideMenuItem.Header = "enable toolbar autohide";
				ToolbarAutohider.StopWatching();
			}
			EventBeacon.SendEvent(Events.UpdateSettings);
		}

		private void OnPerAppLayoutsToggle(object sender, RoutedEventArgs e)
		{
			AppState.Settings.PerAppLayoutsEnabled = !AppState.Settings.PerAppLayoutsEnabled;

			if (AppState.Settings.PerAppLayoutsEnabled)
			{
				_perAppLayoutsMenuItem.Header = "disable per-app layouts";
			}
			else
			{
				_perAppLayoutsMenuItem.Header = "enable per-app layouts";
			}
			EventBeacon.SendEvent(Events.UpdateSettings);
		}


		private void OnAbout(object sender, RoutedEventArgs e)
		{
			var startInfo = new ProcessStartInfo()
			{
				Arguments = AppState.LayoutsRoot,
				FileName = "http://github.com/Martenfur/TabletFriend",
				UseShellExecute = true,
			};
			Process.Start(startInfo);
		}

		private MenuItem AddMenuItem(string header, RoutedEventHandler click = null)
		{
			var item = new MenuItem() { Header = header };
			if (click != null)
			{
				item.Click += click;
			}
			else
			{
				item.IsEnabled = false;
			}
			_menu.Items.Add(item);
			return item;
		}

		private MenuItem AddSubmenuItem(MenuItem menu, string header, RoutedEventHandler click = null)
		{
			var item = new MenuItem() { Header = header };
			if (click != null)
			{
				item.Click += click;
			}
			else
			{
				item.IsEnabled = false;
			}
			menu.Items.Add(item);
			return item;
		}

		private void OnOpenLayoutsDirectory(object sender, RoutedEventArgs e)
		{
			var startInfo = new ProcessStartInfo()
			{
				Arguments = Path.Combine(AppState.CurrentDirectory, "files"),
				FileName = "explorer.exe"
			};
			Process.Start(startInfo);
		}


		private async void OnQuit(object sender, RoutedEventArgs e)
		{
			await AppLifecycle.ShutdownAsync();
		}

		private bool _isLightTheme
		{
			get
			{
				var key = Registry.GetValue(
					@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", 
					"SystemUsesLightTheme", 
					0
				);
				return !(key == null || (int)key == 0);
			}
		}

	}
}
