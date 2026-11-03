using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace TabletFriend.Tests
{
    public class FileManagerTests
    {
        [Fact]
        public void CustomOverridesReloadAndDeletionRestoresBundledLayoutAndTheme()
        {
            Sta.Run(async () =>
            {
                using var folder = new TestDirectory();
                var layouts = Directory.CreateDirectory(Path.Combine(folder.Path, "layouts")).FullName;
                var themes = Directory.CreateDirectory(Path.Combine(folder.Path, "themes")).FullName;
                var catalog = new BundledFiles(new Dictionary<string, byte[]>
                {
                    ["layouts/default.yaml"] = Encoding.UTF8.GetBytes("button_size: 20\nbuttons: {}\n"),
                    ["themes/default.yaml"] = Encoding.UTF8.GetBytes("rounding: 5\n")
                });
                var path = Path.Combine(layouts, "default.yaml");
                var themePath = Path.Combine(themes, "DEFAULT.yaml");
                File.WriteAllText(path, "button_size: 30\nbuttons: {}\n");
                File.WriteAllText(themePath, "rounding: 10\n");
                using var manager = new FileManager(folder.Path, Dispatcher.CurrentDispatcher, catalog);
                Assert.Single(AppState.Layouts);
                Assert.Single(AppState.Themes);
                Assert.Equal(30, AppState.Layouts["default"].ButtonSize);
                Assert.Equal(10, AppState.Themes["default"].Rounding);
                var original = AppState.Layouts["default"];
                var errors = new List<string>();
                manager.Errors += errors.Add;
                File.WriteAllText(path, "buttons:\n  invalid:\n    size: 0,1\n");
                await WaitUntil(() => errors.Count > 0);
                Assert.Same(original, AppState.Layouts["default"]);
                File.Delete(path);
                File.Delete(themePath);
                await WaitUntil(() => AppState.Layouts["default"].ButtonSize == 20 && AppState.Themes["default"].Rounding == 5);
                Assert.Equal("default", AppState.CurrentLayoutName);
                File.WriteAllText(path, "button_size: 40\nbuttons: {}\n");
                await WaitUntil(() => AppState.Layouts["default"].ButtonSize == 40);
                // Imports are preserved for review, never added to the live menus.
                var imports = Directory.CreateDirectory(Path.Combine(folder.Path, "Imports", "old", "layouts")).FullName;
                File.WriteAllText(Path.Combine(imports, "review.yaml"), "buttons: {}\n");
                manager.NotifyChanged(Path.Combine(imports, "review.yaml"));
                await manager.Completion;
                Assert.False(AppState.Layouts.ContainsKey("review"));
                manager.Dispose();
                await manager.Completion;
            });
        }

        [Fact]
        public void WatcherPublishesRenameDeletionAndSelectionChangesOnDispatcher()
        {
            Sta.Run(async () =>
            {
                using var folder = new TestDirectory();
                var layouts = Directory.CreateDirectory(Path.Combine(folder.Path, "layouts")).FullName;
                Directory.CreateDirectory(Path.Combine(folder.Path, "themes"));
                var old = Path.Combine(layouts, "old.yaml");
                File.WriteAllText(old, "buttons: {}\n");
                using var manager = new FileManager(folder.Path, Dispatcher.CurrentDispatcher);
                AppState.CurrentLayoutName = AppState.LastManuallySetLayout = "old";
                var events = new List<string>();
                var dispatcher = Dispatcher.CurrentDispatcher;
                Action<object[]> layoutsChanged = _ => events.Add("layouts");
                Action<object[]> filesChanged = _ =>
                {
                    Assert.True(dispatcher.CheckAccess());
                    events.Add("files");
                };
                EventBeacon.Subscribe(Events.UpdateLayoutList, layoutsChanged);
                EventBeacon.Subscribe(Events.FilesChanged, filesChanged);
                try
                {
                    var renamed = Path.Combine(layouts, "renamed.yaml");
                    File.Move(old, renamed);
                    await WaitUntil(() => AppState.Layouts.ContainsKey("renamed"));
                    Assert.False(AppState.Layouts.ContainsKey("old"));
                    Assert.Equal("renamed", AppState.CurrentLayoutName);
                    Assert.Equal("renamed", AppState.LastManuallySetLayout);
                    Assert.True(events.IndexOf("layouts") < events.IndexOf("files"));
                    File.Delete(renamed);
                    await WaitUntil(() => AppState.Layouts.Count == 0);
                    Assert.Equal("default", AppState.CurrentLayoutName);
                    manager.Dispose();
                    await manager.Completion;
                    File.WriteAllText(Path.Combine(layouts, "late.yaml"), "buttons: {}\n");
                    manager.NotifyChanged(Path.Combine(layouts, "late.yaml"));
                    await manager.Completion;
                    Assert.Empty(AppState.Layouts);
                }
                finally
                {
                    EventBeacon.Unsubscribe(Events.UpdateLayoutList, layoutsChanged);
                    EventBeacon.Unsubscribe(Events.FilesChanged, filesChanged);
                }
            });
        }

        [Fact]
        public void BadEditsRetainModelAndReportOnceUntilChangedOrRecovered()
        {
            Sta.Run(async () =>
            {
                using var folder = new TestDirectory();
                var layouts = Directory.CreateDirectory(Path.Combine(folder.Path, "layouts")).FullName;
                Directory.CreateDirectory(Path.Combine(folder.Path, "themes"));
                var path = Path.Combine(layouts, "custom.yaml");
                File.WriteAllText(path, "buttons:\n  a:\n    size: 1,1\n");
                using var manager = new FileManager(folder.Path, Dispatcher.CurrentDispatcher);
                var original = AppState.Layouts["custom"];
                var errors = new List<string>();
                manager.Errors += errors.Add;
                File.WriteAllText(path, "buttons:\n  a:\n    size: 0,1\n");
                await WaitUntil(() => errors.Count > 0);
                await manager.Completion;
                Assert.Same(original, AppState.Layouts["custom"]);
                Assert.Single(errors);
                manager.NotifyChanged(Path.Combine(folder.Path, "unrelated.png"));
                await manager.Completion;
                Assert.Single(errors);
                File.WriteAllText(path, "buttons:\n  a:\n    size: 2,1\n");
                await WaitUntil(() => AppState.Layouts["custom"].Buttons[0].Size.X == 2);
                Assert.NotSame(original, AppState.Layouts["custom"]);
                manager.Dispose();
                await manager.Completion;
            });
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(6);
            while (!condition())
            {
                Assert.True(DateTime.UtcNow < deadline, "File watcher did not publish expected state.");
                await Task.Delay(10);
            }
        }
    }
}
