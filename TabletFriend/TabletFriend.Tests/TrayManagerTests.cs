using System;
using System.Windows.Controls;
using Xunit;

namespace TabletFriend.Tests
{
    public class TrayManagerTests
    {
        [Fact]
        public void RebuildingMenusDoesNotAddFocusSubscriptions()
        {
            Sta.Run(() =>
            {
                AppState.Settings = new Settings();
                var monitor = new FakeFocusMonitor();
                var tray = new TrayManager(() => new MenuItem(), () => new MenuItem(), monitor);
                for (var i = 0; i < 100; i++) tray.RebuildMenu();
                Assert.Equal(1, monitor.SubscriberCount);
                monitor.Publish("paint");
                Assert.Equal("focused app: paint", tray.FocusedAppItem.Header);
                Assert.Equal(1, monitor.Deliveries);
                tray.Dispose();
                tray.Dispose();
                Assert.Equal(0, monitor.SubscriberCount);
                monitor.Publish("other");
                Assert.Equal(1, monitor.Deliveries);
            });
        }

        private sealed class FakeFocusMonitor : IAppFocusMonitor
        {
            public event Action<string> OnAppChanged;
            public string FocusedApp { get; private set; }
            public int SubscriberCount => OnAppChanged?.GetInvocationList().Length ?? 0;
            public int Deliveries { get; private set; }
            public void Publish(string app)
            {
                FocusedApp = app;
                Deliveries += SubscriberCount;
                OnAppChanged?.Invoke(app);
            }
        }
    }
}
