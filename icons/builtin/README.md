# Built-in Tablet Friend icons

The Affinity documents are the editable source files:

- [brush-icons.af](brush-icons.af) contains the `brush-plus` and `brush-minus` icons.
- [opacity-icons.af](opacity-icons.af) contains the `opacity-plus` and
  `opacity-minus` icons.

The SVGs alongside them are exports of these four designs. Layouts select the
compiled icons by name, for example:

```yaml
icon: brush-plus
```

Names are case-insensitive and accept hyphens, underscores, or neither. The icon
only supplies the artwork; the button's `action` supplies the application's
increase/decrease shortcut. Existing Material Design icons remain available.

The application renders the compiled geometries in
`TabletFriend/TabletFriend/Icons/BuiltInIcons.xaml`, using the button's foreground
colour. No external SVG files or SVG rendering dependency are needed at runtime.
The default size is 24 x 24, matching the Material icons.

Edit the `.af` documents in Affinity, export the four SVGs into this directory,
and update the matching XAML geometry too. Commit the source documents, exports,
and geometry changes together. Preserve the
128 x 128 viewBox, group matrices (including nested translations), and nonzero
path fill rules. SVG path data maps to WPF geometry data prefixed with `F1`;
rectangles map to `RectangleGeometry`. The white SVG fill is intentionally
replaced with the theme foreground at render time.
