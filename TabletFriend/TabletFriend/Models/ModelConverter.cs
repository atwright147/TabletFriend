using System.Numerics;

namespace TabletFriend.Models
{
	using System;
	using System.Globalization;
	public static class ModelConverter
	{
		public static Vector2 ConvertVector2(string str)
		{
			if (str == null) return Vector2.One;
			var parts = str.Split(',');
			if (parts.Length != 2 ||
				!int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var x) ||
				!int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var y) ||
				x <= 0 || y <= 0)
				throw new FormatException("Size must contain exactly two positive integers, for example '2,1'.");
			return new Vector2(x, y);
		}
	}
}
