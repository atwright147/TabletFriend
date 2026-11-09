# Reliability regression tests

Use the Windows .NET 10 SDK (`10.0.403` or a newer `10.0.4xx` patch, as selected by `global.json`). The SDK includes the .NET 10 Windows Desktop runtime required by the application and tests, which target `net10.0-windows`.

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

Coverage includes menu subscription lifetime; button/spacer dimensions and bundled layouts; last-valid configuration retention; rename/deletion and fallback selection; real filesystem notifications; debounce and stale-result rejection; disposal; input serialization/failures; cancellation of nested action batches; embedded defaults and custom overrides; and migration classification, conflicts, retries, repeat runs, and preservation of original files and installer backups. Migration and backup tests use temporary directories, never the real Documents or installed application folders.

For desktop acceptance testing, use a disposable Windows test account: installed and portable builds share that account's real Documents\TabletFriend folder and scan its old AppData installation. Run a separate copy of the build with `add_to_autostart`, `first_launch`, and `update_checking_enabled` set to `false` in its settings. Open the toolbar through its tray icon, then check:

- Editing, renaming, and deleting layouts refreshes both menus without freezing the toolbar.
- Invalid edits, including misspelled fields and unsupported button styles, retain the displayed toolbar and produce a nonmodal error; correcting the file reloads it.
- Layout/theme switching, per-app layouts, and docking still render correctly.
- With per-app layouts enabled, focus an app with custom-drawn controls such as Clip Studio Paint without clicking a tool. Confirm its layout appears, then focus another app and confirm the manually selected layout returns. Repeat with the app already focused when the toolbar starts or its layout file reloads.
- With toolbar autohide disabled, verify the toolbar is visible at startup and stays visible through both desktop-to-tablet and tablet-to-desktop transitions. Hide it manually through the tray and verify a posture change does not reveal it. Enable autohide to check the optional mode-based behavior, then disable it while hidden and verify it immediately reappears. Restart to check the preference is retained.
- With the toolbar docked left/right, switch desktop/tablet posture repeatedly and expand/collapse the tablet taskbar. Check the toolbar fills the current work area down to the collapsed taskbar without a gap and its buttons stay centered in the available height; the temporarily expanded taskbar may overlay the toolbar just as it overlays other applications. Repeat with taskbar auto-hide, top/bottom docking, display rotation/scaling, and rapid hide/show or undock/redock during a transition. Change posture while hidden, then show the toolbar; verify the current bounds and that undocked windows are not moved by late docking updates.
- Both tray folder commands open the expected locations, including redirected Documents and paths with spaces. Copy a bundled layout/theme into the custom folder, edit it, and verify it overrides the bundled version. Delete the override and verify the bundled version returns; malformed overrides retain the last valid model.
- Before the first run, put a new layout/theme/icon, a known stock layout, and an edited stock layout in the old installation's files folder and files.backup. Verify custom names import, known stock files are skipped, and uncertain files/conflicts go into Imports with reports and source images. Originals and existing user edits must survive. Restart and verify intentionally deleted imports are not recreated; updates must retain earlier backups. Check migration notices and recovery after a locked source file is released.
- Custom PNG and SVG paths render side by side, including paths with spaces and uppercase `.SVG` extensions. Check `icon_stretch: none`, `fit`, and `fill`, SVG colours and transparent padding, and launching from another working directory. After replacing an SVG, save the layout to reload it; malformed SVGs should retain the previous toolbar and report the affected icon path, then recover after correction and another layout save.
- Toggle Ctrl or explicitly hold Shift, quit through the tray, and confirm normal typing in a neutral editor.
- Copy the sample layout into the disposable account's custom folder and test its Ctrl press-and-hold button with mouse, touch, and pen. Verify immediate modifier activation while drawing or pressing another toolbar button, quick-tap lock/unlock, and release after a long press (including from locked state). Repeat with `quick_press_toggle: false`, and with a plain chord such as `ctrl+shift`. Check the held-state highlight and a physical key release.
- Hold a finger on the Ctrl icon for several seconds while dragging the pen left/right on a disposable Rebelle canvas. Verify brush resizing, no toolbar context menu during or after lifting the finger, and release of Ctrl on lifting after a long hold. Repeat over the button background and text, and with pen input on the modifier. Check that a deliberate mouse right-click still opens the toolbar menu when no press is in progress. Hardware touch/pen coexistence requires this manual check; routed-event tests do not emulate tablet drivers or palm rejection.
- For press-and-hold buttons, release outside the control and interrupt pointer capture: neither should lock the key. Hold two touch buttons sharing a modifier and verify lifting one does not release the other's key. Check that touch/pen events do not also produce a duplicate mouse action. Hide/minimize the toolbar, reload a layout/theme, change docking, and quit while a key is held or tap-locked; confirm release in a neutral editor. Repeat quick taps during slow actions and verify no delayed stuck keys. Do not run these checks against an unsaved document.
- Closing during a waiting action batch cancels subsequent steps; no stale tray icon remains. Check tray Quit with the toolbar hidden and visible, and immediately after launch. Switch focus between apps before quitting; the process should exit without freezing, including with per-app layouts enabled.

Forced process termination is outside the graceful key-release guarantee. Never test held keys against an unsaved working document.

Historical bundled fingerprints are committed in `TabletFriend/TabletFriend/Migration/bundled-hashes.json`. After fetching additional historical release commits, run `pwsh -File scripts/Update-BundledHashes.ps1` from the repository root to add their fingerprints. The script retains previously known hashes, including in shallow checkouts. Current bundled files are fingerprinted directly from embedded resources, so editing a bundled example does not require regenerating the historical manifest.
