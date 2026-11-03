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
		internal static ImageSource Load(string path)
		{
			if (!string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase))
			{
				using var stream = File.OpenRead(path);
				var bitmap = new BitmapImage();
				bitmap.BeginInit();
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
				bitmap.StreamSource = stream;
				bitmap.EndInit();
				bitmap.Freeze();
				return bitmap;
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
