# Tablet Friend

![logo](icons/logo.png)

### [DOWNLOAD](/TabletFriend/releases/latest)

### [SAMPLE LAYOUT](TabletFriend/TabletFriend/files/layouts/sample_layout.yaml)

### [VIDEO GUIDE](https://www.youtube.com/watch?v=rWkRrhrKmSY)

Hey there! Working without a keyboard is hard. When you are on a tablet, you realize how much you miss certain key combinations. Well, no more! Tablet Friend will make your life on Surface Pro or any other Windows tablet easier by providing a highly customizable set of on-screen toolbars.

Whether you are an artist, a casual Windows tablet fan or simply a touchscreen enjoyer, Tablet Friend is the perfect companion for getting stuff done. 

![showoff](docs/pics/showoff.png)

Here's what Tablet Friend can do:

- Press buttons and button combinations.
- Toggle buttons.
- Type.
- Press and hold buttons.
- Execute command line commands - this allows you to launch any programs you wish or make custom scripts that do exactly what you want. Batch, Powershell - the sky is the limit.
- Swap and link layouts seamlessly.
- Chain any number of actions.
- Use over 4000 built-in vector icons from [https://materialdesignicons.com](https://materialdesignicons.com) or your own PNG and SVG icons.
- Dock the toolbar to the side or on top.
- Simply look nice. :)



## Getting started

Grab the latest release from [this page](/TabletFriend/releases/latest), unzip it... and that's it! No multistep installations, no hassle, you can start using the thing right away. 

Toolbar autohide is enabled by default: the toolbar follows Windows' tablet-mode signal to show in tablet mode and hide in desktop mode. To keep it visible in both modes, right-click its tray icon and choose **settings > disable toolbar autohide**. Left-click the tray icon to hide or show it manually.

## Press and hold, with optional tap locking

Add `press_and_hold: true` to a plain key button to hold its key or chord while your finger, pen, or left mouse button is down. Add `quick_press_toggle: true` to let a quick tap keep it enabled:

```yaml
buttons:
  ctrl:
    action: ctrl
    text: Ctrl
    press_and_hold: true
    quick_press_toggle: true
    quick_press_threshold_ms: 250
```

The key goes down immediately. A tap shorter than 250 ms locks it on; another tap turns it off. A longer press releases on lifting, even if the key was already locked. The button highlights while its keys are held. Set `quick_press_toggle: false` (the default) for hold-only behavior. The optional threshold is a positive number of milliseconds; exactly the threshold counts as a long press.

Use plain actions such as `ctrl`, `space`, or `ctrl+shift`. This mode does not accept prefixed actions (`toggle`, `hold`, `repeat`, etc.) or `actions` batches. Quick-tap locking requires `press_and_hold: true`. Existing buttons are unchanged unless you opt in; existing `hold`/`release` actions remain available for batches.

Releasing outside the button or losing pointer capture cancels the press without locking. This mode's held and locked keys release when the toolbar hides, minimizes, or rebuilds (including layout/theme changes), or when the app quits normally. Simultaneous buttons can share a key without releasing each other's hold.

## Making your own toolbars

Custom files live in your Windows **Documents\TabletFriend** folder, independently of where the app is installed or extracted. Redirected Documents folders (including OneDrive) are supported. Put layouts in `layouts`, themes in `themes`, and images in `icons`. The same custom files are used by installed and portable copies running under your Windows account.

Bundled layouts and themes remain available. A custom `.yaml` file with the same name overrides the bundled version; deleting that custom file restores the bundled version. To edit an example, use **settings > open bundled examples...** and copy it into the corresponding custom folder first.

To use a custom icon, put a PNG or SVG file in `Documents\TabletFriend\icons` and set the button's `icon` to its path, for example `icon: icons/my-icon.svg` or `icon: icons/my-icon.png`. Relative paths in custom layouts start from `Documents\TabletFriend`. Absolute paths and existing application-relative paths still work, including the legacy `files/icons/...` prefix. Custom images take precedence over application images for custom layouts. SVGs keep their original colours. Both formats support `icon_stretch: fit` (preserve proportions), `fill` (fill the button), and `none` (original size, the default). After changing an image, save the layout file again to reload it.

On startup, Tablet Friend checks the running copy's old `files` folder and backups, plus `%APPDATA%\TabletFriend\files` and its backups. Known unchanged bundled files are skipped using hashes of current and historical versions (YAML line endings are normalized). Custom filenames are copied into Documents. Files with a bundled filename but unrecognized contents, conflicting files, and layouts from sources with conflicting icon assets are copied under `Documents\TabletFriend\Imports` for review. Each import folder includes its original location, a report, and the source images. Review these files, then copy the layouts/themes you want into the live custom folders and copy any required icons, adjusting paths for name conflicts. Review copies are not loaded automatically. Existing user files and originals are preserved.

Migration runs once per legacy folder; failed copies retry on restart. Later edits should go in Documents. A startup tray notification gives the new location and migration counts; the tray menu can open the custom folder at any time. These migration checks do not search unrelated folders elsewhere on your computer.

Let's be honest - the default toolbars will probably not be enough for you. But that's ok. It's you who knows best what you need, this is why Tablet Friend is a great toolbar constructor first and a great toolbar second.

First, right-click the tray icon, open `settings`, and press **open custom files directory...**.

![menu](docs/pics/menu.png)

This opens `Documents\TabletFriend`. Inside it, the `layouts` folder contains your custom toolbar `.yaml` files. You can edit them with any text editor or copy a bundled example there to get started.

Let's make a new toolbar - create a file named `my_toolbar.yaml` in the `layouts` directory, open it and paste this inside:

```yaml
buttons:
	cut_button:
		action: ctrl+x
		text: cut
 ```

Yes, it's that simple. Let's break down what we just wrote: `buttons` is a collection of buttons. This is where all your buttons will go. `cut_button` is the name of your button. **Note that all button names should be unique.** Its `action` is a ctrl+x press. And it will display `text` that says "cut" on it. Now, right-click Tablet Friend toolbar and choose "my toolbar" from the list. You don't need to relaunch the program - it updates everything automatically. If you did everything correctly, you will see this:

![s1](docs/pics/s1.png)

In just four lines, we got ourselves a working button! But obviously, this is not enough for a functional toolbar. Let's add some more and some cosmetics:

```yaml
buttons:
	cut_button:
		action: ctrl+x
		text: cut
	copy_button:
		action: ctrl+c
		text: copy
	paste_button:
		action: ctrl+v
		text: paste
		size: 2,1	
 ```

Now, press `Ctrl+S` and your toolbar will update automatically. Magic!

![s2](docs/pics/s2.png)

Paste button is wider that the others, because its `size` is 2 cells wide and 1 cell high. You also may wonder how do you specify button positions. That's the best thing - you don't. Instead of tediously calculating all the positions by hand, you let Tablet Friend handle this for you. 

All buttons are put on the layout from left to right in a single line - until they run out of space. Every layout has a property called 'layout_width'. It tells the layout how many cells it has before it will be forced to put buttons on a second line. By default, this value is 2, but you can change it. Simply paste this on the very first line of your config:

```yaml
layout_width: 4
```

Press Ctrl+S and your layout will change to this:

![s3](docs/pics/s3.png)

Because there is enough space now, all buttons can fit on the same line. You can play around and see what happens at different button sizes, layout width and button amounts. This system is very intuitive once you get the hang of it.

However, what if you want to space out your buttons, or create a specific shape out of your buttons? For that, Tablet Friend has spacers. Change your config to look like this:

```yaml
layout_width: 2
buttons:
	cut_button:
		action: ctrl+x
		text: cut
	copy_button:
		action: ctrl+c
		text: copy
	spacer1:
		spacer: true
		size: 2,1
	paste_button:
		action: ctrl+v
		text: paste
		size: 2,1	
		style: accent # Let's also change this button's style to something different.
 ```

Press Ctrl+S and your toolbar will now have a line of space in-between the buttons:

![s4](docs/pics/s4.png)

`Spacer` stacks just like buttons, but instead of a button, it creates empty space. The only valid property for it is `size`. Using spacers, you can create absolutely any shape and layout you want. 

It is **VERY IMPORTANT** that you use tab characters `	`  instead of spaces for your indents. Indents do matter, since they tell the config what should go where. You *can* use spaces if you really want to, but keep in mind that all the default layouts use tabs, and tabs and spaces **should never mix** in one config. 

### App-specific layouts

Having a layout is great but what if you want to have one layout for your drawing app, another for note taking and a completely separate one for web browsing? You're in luck because since version 2.0, Tablet Friend has app-specific layout support!

Simply add this line to the top of your layout config:
```yaml 
app: mspaint
```
And now, every time we open Microsoft Paint, the layout will automatically switch to this one. But how do we know what exact name an app has? 
For this, you must focus on the app you want, right-click the Tablet Friend tray icon, select `settings` and look at the `focused app` label. It always tells the app you're focusing on. 
![app_name](docs/pics/app_name.png)

You can also use wildcards:
```yaml
app: app_name     # Will match only app_name exactly.
app: app_name*    # Will match any app name that starts with app_name. For example, app_name_v_1.0
app: "*app_name*" # Will match any app name that contains app_name. Note that if you're putting a * symbol in the beginning, you need to use quotes. 
```

If you switch to an app that doesn't have an app-spesific layout, it will switch to the last manually selected layout. 

You can also disable automatic switching altogether by going into `settings` and pressing the `disable per-app layouts` button.

**NOTE**: In order for transition between layouts to be smooth and quick, `layout_width`, `button_size` and `margin` should be exactly the same between layouts.

This, of course, are not all the features Tablet Friend offers. You can check out a more [advanced example](TabletFriend/TabletFriend/files/layouts/sample_layout.yaml) with all features listed.

## Migrating to 2.0

Starting with the version 2.0.0, layout-specific themes are no longer supported. Instead, you can choose a theme in the context menu that will be applied to all toolbars. 

![themes](docs/pics/themes.png)

This means, the layout structure has changed a little bit. 

- Layout properties `external_theme` and `theme` no longer work.
- Theme properties `button_size`, `margin`, `min_opacity`, `max_opacity` have been moved directly to layout yaml. Themes can no longer influence these properties.

An example of 1.0 to 2.0 migration for a layout:

```yaml
# old layout
layout_width: 4
external_theme: files/themes/default.yaml
theme:
	button_size: 14

buttons:
	hide:
		action: a
		text: "a"

# new layout
layout_width: 4
button_size: 14
margin: 8

buttons:
	hide:
		action: a
		text: "a"

```

Note that unmodified 1.0 layouts and themes will still function but may get rendered differently.

## Release builds

The [release workflow](.github/workflows/release.yml) runs when a GitHub release is published, including pre-releases and releases published from drafts, or when started manually. Pushes, pull requests, tag creation alone, and draft saves do not trigger builds. For automatic release builds, the release's tagged commit must contain the workflow.

After the regression tests pass on Windows x64, the workflow publishes self-contained builds for `win-x64` and `win-arm64` and attaches `TabletFriend-win-x64.zip` and `TabletFriend-win-arm64.zip` to the release. Both packages include the .NET runtime and the bundled layouts, themes, and icons. Extract the entire ZIP before running `TabletFriend.exe`. The ARM64 package is cross-compiled on the x64 runner; the workflow does not run ARM64 desktop acceptance tests.

The workflow uses the SDK selected by `global.json` and the built-in `GITHUB_TOKEN` to upload release assets; no additional secret is required. Re-running a release build replaces its existing ZIP assets.

To rebuild an existing release with new changes, commit and push those changes, then open **Actions → Build release → Run workflow**. Select the branch to build and enter the existing release tag in `release_tag`. The workflow checks that the release exists before testing and building. The manual trigger must first be committed to the repository's default branch to make this option available, and the selected branch must also contain it.

You can also start it with GitHub CLI (replace `YOUR_BRANCH` and `EXISTING_TAG`):

```powershell
gh workflow run release.yml --ref YOUR_BRANCH -f release_tag=EXISTING_TAG
```

Manual runs build the selected branch's commit and replace the target release's ZIPs without moving its tag or changing its source archives. The rebuilt binaries may therefore differ from the tagged source. **Re-run all jobs** repeats the original commit; use **Run workflow** to include new commits. Builds targeting the same release are serialized to avoid overlapping uploads.

## Building from source

Build on Windows with the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) for your processor architecture. The repository selects SDK `10.0.403` in `global.json` and accepts newer patches within the `10.0.4xx` feature band. Preview SDKs are disabled. The application and tests target `net10.0-windows` and require the .NET 10 Windows Desktop runtime, which the Windows .NET 10 SDK includes. A runtime-only installation cannot build the project.

Install [Task](https://taskfile.dev/docs/installation), then reopen your terminal:

```powershell
winget install Task.Task
```

From the repository root:

```powershell
task --list     # Show available tasks
task build      # Restore and build in Release mode (also the default task)
task test       # Restore, build, and run regression tests
task run        # Build and launch; open the toolbar through its tray icon
```

`task restore` restores packages without building. `task clean` cleans generated build outputs. To use Debug mode, run `task build CONFIGURATION=Debug` (the same setting works for test, run, and clean).

The Release executable is `TabletFriend/TabletFriend/bin/Release/net10.0-windows/TabletFriend.exe`. Keep the adjacent DLLs and `files` directory together when running it. This build requires the .NET 10 Windows Desktop runtime; it is not a self-contained distribution.

If your SDK is not on `PATH`, pass its executable explicitly:

```powershell
task build "DOTNET=C:/path/to/dotnet.exe"
```

Task is optional; the equivalent build command is:

```powershell
dotnet build TabletFriend/TabletFriend.sln --configuration Release
```

See [testing instructions](docs/testing.md) for test coverage and desktop checks.

## Making Tablet Friend better

Tablet Friend is opensource, licensed under MIT and 100% free. You can help out the development by submitting your own toolbars to the [issues](/TabletFriend/issues) board or helping out with the code. 

If you have any questions, you can contact me at `chaifoxes@gmail.com`
