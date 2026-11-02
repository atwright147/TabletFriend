using TabletFriend.Models;

namespace TabletFriend
{
    public class ThemeManager
    {
        private static readonly ThemeModel Fallback = new ThemeModel();
        public ThemeManager()
        {
            EventBeacon.Subscribe(Events.FilesChanged, OnFilesChanged);
            EventBeacon.Subscribe(Events.ChangeTheme, OnChangeTheme);
        }
        private void OnFilesChanged(object[] args) => LoadTheme(AppState.CurrentThemeName);
        private void OnChangeTheme(object[] args)
        {
            var first = AppState.CurrentTheme == null;
            LoadTheme((string)args[0]);
            if (!first) EventBeacon.SendEvent(Events.UpdateSettings);
        }
        public void LoadTheme(string path)
        {
            var name = ConfigurationSelection.Select(AppState.Themes, path);
            AppState.CurrentTheme = AppState.Themes.TryGetValue(name, out var theme) ? theme : Fallback;
            AppState.CurrentThemeName = name;
        }
    }
}
