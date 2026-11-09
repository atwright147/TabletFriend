# [Changelog](http://keepachangelog.com/en/1.0.0/):

## [Unreleased]

### Added

- Added a JSON Schema and VS Code YAML association for layout completion and early validation, with instructions for personal layouts.
- Added opt-in press-and-hold buttons for mouse, touch, and pen, with optional quick-tap locking and a configurable tap threshold. Enabled native Windows pointer input to receive touch contact durations. Touch/pen holds suppress the toolbar context menu while intentional mouse right-clicks remain available. Held keys release when these controls hide or rebuild and during graceful shutdown.
- Added custom SVG button icons alongside PNGs, using the same `icon` paths and `icon_stretch` options while preserving SVG colours and vector scaling.
- Added built-in `brush-plus`, `brush-minus`, `opacity-plus`, and `opacity-minus` vector icons that follow the button's theme colour and can be selected by name in any layout. Editable Affinity sources and SVG exports are included in `icons/builtin`.
- Added a GitHub Actions workflow that tests and builds self-contained Windows x64 and ARM64 packages when a release is published, then attaches the ZIPs to that release. Manual runs can rebuild a selected branch and replace the ZIPs on an existing release without creating a new tag.
- Added reliability regression tests, testing documentation, and Task commands for building, testing, and running the app.
- Added layout validation with clearer errors for invalid button and spacer sizes.

### Changed

- Layout loading now rejects unknown fields, invalid numeric settings, unsupported styles, conflicting actions, and action settings on spacers while retaining the last valid layout after a bad edit. Existing personal layouts with these values need correction.
- Custom layouts, themes, and icons now live in Windows Documents\TabletFriend, shared by installed and portable copies. User layouts and themes override bundled files by name; remove an override to restore the bundled version. The tray menu opens custom files and bundled examples separately.
- Legacy files and backups are copied on first use into the user folder without overwriting existing files. Known bundled versions are skipped; uncertain or conflicting files are preserved under Imports with reports for review. Original files remain intact, and repeated installations keep separate backups.
- Updated the app and tests to .NET 10 and pinned the SDK through `global.json`.
- Layout and theme changes now reload asynchronously, combine rapid file changes into one reload, and ignore outdated results.
- Configuration and action errors now appear as nonmodal tray notifications.

### Fixed

- Disabling toolbar autohide now immediately restores a hidden toolbar, and the toolbar opens visibly on startup when autohide is disabled.
- Docked toolbars now resize and recenter to the current work area when the taskbar changes height between desktop and tablet modes, including when Windows reserves the expanded tablet-taskbar height while showing its collapsed version. Rapid docking or visibility changes no longer apply stale sizes or accumulate Windows message hooks.
- Custom icon paths now resolve consistently from the application directory even when Tablet Friend is launched from another working directory.
- PNG icons no longer keep their source files locked or show cached contents after the image is replaced and the layout is reloaded.
- Invalid layout and theme edits now retain the last valid configuration. Renamed or deleted files refresh the menus and select an available fallback when needed.
- Held and toggled keys now share consistent state tracking and are released during graceful shutdown. Pending action batches are cancelled when closing the app.
- Fixed repeated tray menu event subscriptions and cleaned up file watchers, timers, and focus monitoring on shutdown. Focus monitoring now registers and unregisters off the UI thread so Quit can finish without freezing.
- Buttons and spacers without an explicit size now default to `1,1`.
- Installation now shuts down the original instance gracefully after a successful move to AppData and keeps it running if installation fails.

## [v2.0.0] - 25.10.2023

### Added

- Added default Krita, Photoshop, ArtRage and Leonardo layouts.
- Now, the toolbar will automatically show up when you enter tablet mode, and hide when you enter desktop mode. This can be disabled in the settings.
- Added `dock` and `undock` actions that allow to perform toolbar docking with a button instead of a context menu.
- Added `docked_right`, `docked_left` and `docked_top` options to `visibility` property. Now, buttons can be shown only in some docking modes.

### Changed

- `layout` action now only needs layout name to be specified instead of its full path (for example, `action layout foxe` instead of `action layout files/layouts/foxe.yaml`). Paths will still work as long as the layout specified is in the `files/layouts` directory.
- Layout properties `external_theme` and `theme` no longer work. Now, you must select a theme in the context menu independently from a layout. See "Migrating to 2.0" section for the full guide.
- Theme properties `button_size`, `margin`, `min_opacity`, `max_opacity` have been moved directly to layout yaml. Themes can no longer influence these properties. See "Migrating to 2.0" section for the full guide.
- Now the app will start in hidden mode.
- Updated to .NET7
- Optimized layout changing.

### Fixed

- Context menu color will now match theme color.
- Quit button doesn't hang the app anymore.

## [v1.2.0] - 24.10.2021

### Added

- Added `hide` action which hides the toolbar.

### Fixed

- Autostart now works properly.

<hr/>

## [v1.1.0] - 06.10.2021

### Added

- Added the titlebar with the ability to minimize the window.
- Added `visibility` attribute - now, some buttons can be present in either docked or undocked mode.
- When updating, the installer will make a backup.
- Toggle buttons now have visible state.
- Added `repeat` action. Repeating buttons will continue triggering key presses if the button is being held down.
- Added update checker.
- Added a layout for Leonardo.

### Changed

- Expanded default layouts.

### Fixed

- Editing a layout no longer throws false errors.
- Installer now copies only its own files.

<hr/>

## [v1.0.0] - 26.09.2021

helo :D
