using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TabletFriend
{
	internal sealed class BuiltInIcon : Control
	{
		private static readonly Dictionary<string, Geometry> _geometries = LoadGeometries();
		private readonly Geometry _geometry;

		static BuiltInIcon()
		{
			ForegroundProperty.OverrideMetadata(typeof(BuiltInIcon),
				new FrameworkPropertyMetadata(SystemColors.ControlTextBrush,
					FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
		}

		private BuiltInIcon(Geometry geometry)
		{
			_geometry = geometry;
			Width = 24;
			Height = 24;
			Focusable = false;
			IsHitTestVisible = false;
		}

		// Names arrive with the same hyphen/underscore normalization as Material icons.
		internal static BuiltInIcon Create(string name)
		{
			return _geometries.TryGetValue(name, out var geometry) ? new BuiltInIcon(geometry) : null;
		}

		private static Dictionary<string, Geometry> LoadGeometries()
		{
			var resources = (ResourceDictionary)Application.LoadComponent(
				new Uri("/TabletFriend;component/Icons/BuiltInIcons.xaml", UriKind.Relative));
			var geometries = new Dictionary<string, Geometry>(StringComparer.OrdinalIgnoreCase);
			foreach (string name in resources.Keys)
			{
				var geometry = (Geometry)resources[name];
				geometry.Freeze();
				geometries.Add(name, geometry);
			}
			return geometries;
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			// Preserve the SVGs' 128 x 128 viewBox, including their original padding.
			var size = Math.Min(ActualWidth, ActualHeight);
			drawingContext.PushTransform(new MatrixTransform(size / 128, 0, 0, size / 128,
				(ActualWidth - size) / 2, (ActualHeight - size) / 2));
			drawingContext.DrawGeometry(Foreground, null, _geometry);
			drawingContext.Pop();
		}
	}
}
