using System;
using System.IO;
using System.Numerics;
using TabletFriend.Models;

namespace TabletFriend
{
    public class LayoutManager
    {
        private static readonly LayoutModel Fallback = new LayoutModel
        {
            Buttons = { new ButtonModel { Text = "No layouts", Size = new Vector2(4, 1) } }
        };
        public LayoutLoadResult LastLoadResult;

        public LayoutManager()
        {
            EventBeacon.Subscribe(Events.FilesChanged, OnFilesChanged);
            EventBeacon.Subscribe(Events.ChangeLayout, OnChangeLayout);
        }

        private void OnFilesChanged(object[] args) => LoadLayout(AppState.CurrentLayoutName, true);

        private void OnChangeLayout(object[] args)
        {
            var name = Path.GetFileNameWithoutExtension((string)args[0]);
            if (args.Length < 2 || (LayoutChangeMethod)args[1] != LayoutChangeMethod.Automatic)
                AppState.LastManuallySetLayout = ConfigurationSelection.Select(AppState.Layouts, name);
            LoadLayout(name);
        }

        public void LoadLayout(string path) => LoadLayout(path, false);

        private void LoadLayout(string path, bool forceRedock)
        {
            var name = ConfigurationSelection.Select(AppState.Layouts, path);
            var layout = AppState.Layouts.TryGetValue(name, out var value) ? value : Fallback;
            var previous = AppState.CurrentLayout;
            LastLoadResult = forceRedock || previous == null || !previous.IsSameWidth(layout)
                ? LayoutLoadResult.RequiresRedock : LayoutLoadResult.Default;
            if (previous != null && previous != layout) previous.Dispose();
            AppState.CurrentLayout = layout;
            AppState.CurrentLayoutName = name;
            if (AppState.LastManuallySetLayout != null)
                AppState.LastManuallySetLayout = ConfigurationSelection.Select(AppState.Layouts, AppState.LastManuallySetLayout);
            EventBeacon.SendEvent(Events.DockingChanged, AppState.Settings.DockingMode);
        }
    }

    public enum LayoutLoadResult { Default, RequiresRedock }
}
