# Reliability regression tests

Use a Windows .NET SDK and the .NET 7 Windows Desktop runtime. The application and tests deliberately retain `net7.0-windows`; this patch does not migrate the runtime.

From the repository root:

```powershell
task test
```

Or without Task:

```powershell
dotnet restore TabletFriend/TabletFriend.sln
dotnet test TabletFriend/TabletFriend.sln -c Release --no-restore
```

The suite uses fake keyboard output, temporary configuration directories, and STA threads for WPF objects. It does not instantiate the main application window, inject real keys, modify autostart, or invoke the installer/updater.

Coverage includes menu subscription lifetime; button/spacer dimensions and bundled layouts; last-valid configuration retention; rename/deletion and fallback selection; real filesystem notifications; debounce and stale-result rejection; disposal; input serialization/failures; and cancellation of nested action batches.

For desktop acceptance testing, run a separate copy of the build with `add_to_autostart`, `first_launch`, and `update_checking_enabled` set to `false` in its settings. Open the toolbar through its tray icon, then check:

- Editing, renaming, and deleting layouts refreshes both menus without freezing the toolbar.
- Invalid edits retain the displayed toolbar and produce a nonmodal error; correcting the file reloads it.
- Layout/theme switching, per-app layouts, and docking still render correctly.
- Toggle Ctrl or explicitly hold Shift, quit through the tray, and confirm normal typing in a neutral editor.
- Closing during a waiting action batch cancels subsequent steps; no stale tray icon remains.

Forced process termination is outside the graceful key-release guarantee. Never test held keys against an unsaved working document.
