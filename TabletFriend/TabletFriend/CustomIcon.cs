using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TabletFriend
{
	internal static class CustomIcon
	{
		internal static ImageSource LoadBundled(string path)
		{
			var content = BundledFiles.Current.ImageContent(path);
			if (content == null) return null;
			using var stream = new MemoryStream(content, false);
			if (!string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase))
				return LoadBitmap(stream);
			using var reader = new FileSvgReader(new WpfDrawingSettings { EnsureViewboxSize = true });
			var drawing = reader.Read(stream) ?? throw new InvalidDataException($"Invalid bundled SVG '{path}'.");
			var image = new DrawingImage(drawing);
			if (image.CanFreeze) image.Freeze();
			return image;
		}

		private static BitmapImage LoadBitmap(Stream stream)
		{
			var bitmap = new BitmapImage();
			bitmap.BeginInit();
			bitmap.CacheOption = BitmapCacheOption.OnLoad;
			bitmap.StreamSource = stream;
			bitmap.EndInit();
			bitmap.Freeze();
			return bitmap;
		}

		internal static string ResolvePath(string path, string userRoot, string applicationDirectory)
		{
			if (Path.IsPathRooted(path)) return path;
			if (userRoot != null)
			{
				var custom = Path.Combine(userRoot, path);
				if (File.Exists(custom)) return custom;
				// Migrated layouts commonly use the old files/icons/... prefix.
				var normalized = path.Replace('\\', '/');
				if (normalized.StartsWith("files/icons/", StringComparison.OrdinalIgnoreCase))
				{
					custom = Path.Combine(userRoot, normalized.Substring("files/".Length));
					if (File.Exists(custom)) return custom;
				}
			}
			return Path.Combine(applicationDirectory, path);
		}

		internal static ImageSource Load(string path)
		{
			if (!string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase))
			{
				using var stream = File.OpenRead(path);
				return LoadBitmap(stream);
			}

			try
			{
				using var reader = new FileSvgReader(new WpfDrawingSettings
				{
					// Keep transparent padding as part of the icon's natural dimensions.
					EnsureViewboxSize = true
				});
				var drawing = reader.Read(path);
				if (drawing == null)
				{
					throw new InvalidDataException("The file does not contain an SVG drawing.");
				}
				var image = new DrawingImage(drawing);
				if (image.CanFreeze) image.Freeze();
				return image;
			}
			catch (Exception e)
			{
				// Let configuration reload retain the last valid layout and report the file.
				throw new InvalidDataException($"Could not load SVG icon '{path}': {e.Message}", e);
			}
		}
	}
}
