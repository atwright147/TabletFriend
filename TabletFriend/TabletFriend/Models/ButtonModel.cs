using MaterialDesignThemes.Wpf;
using System;
using System.IO;
using System.Numerics;
using System.Windows.Controls;
using System.Windows.Media;
using TabletFriend.Actions;
using TabletFriend.Data;

namespace TabletFriend.Models
{
	public class ButtonModel : IDisposable
	{
		public ButtonAction Action;
		public bool PressAndHold;
		public bool QuickPressToggle;
		public int QuickPressThresholdMs = 250;

		public string Text = "";
		public object Icon;

		public Vector2 Position = Vector2.Zero;
		public Vector2 Size = Vector2.One;

		public string Style;

		public string Font;
		public int FontSize;
		public int FontWeight;

		/// <summary>
		/// If true, the button is considered a spacer. All actions will be ignored.
		/// </summary>
		public bool Spacer;

		public ButtonVisibility Visibility;

		public ButtonModel() { }

		public ButtonModel(ButtonData data, string iconRoot = null, bool bundledIcons = false)
		{
			if (data == null)
			{
				return;
			}
			Text = (data.Text ?? "").Replace("\\n", Environment.NewLine);

			if (!string.IsNullOrEmpty(data.Icon))
			{
				var iconPath = CustomIcon.ResolvePath(data.Icon, iconRoot, AppState.CurrentDirectory);
				var bundledSource = bundledIcons ? CustomIcon.LoadBundled(data.Icon) : null;
				if (bundledSource != null || (!string.IsNullOrEmpty(Path.GetExtension(data.Icon)) && File.Exists(iconPath)))
				{
					var ico = new Image();

					ico.Source = bundledSource ?? CustomIcon.Load(iconPath);
					ico.Stretch = (Stretch)data.IconStretch;

					Icon = ico;
				}
				else
				{
					var iconName = data.Icon.Replace("_", "").Replace("-", ""); // Sanitizing the icon.
					Icon = BuiltInIcon.Create(iconName);
					if (Icon == null && Enum.TryParse<PackIconKind>(iconName, true, out var kind))
					{
						var ico = new PackIcon();
						ico.Kind = kind;

						Icon = ico;
					}
				}
			}

			if (data.Size != null)
			{
				Size = ModelConverter.ConvertVector2(data.Size);
			}

			Spacer = data.Spacer;
			PressAndHold = data.PressAndHold;
			QuickPressToggle = data.QuickPressToggle;
			QuickPressThresholdMs = data.QuickPressThresholdMs;

			if (Spacer)
			{
				Action = null;
			}
			else if (data.Actions == null || data.Actions.Length == 0)
			{
				Action = ButtonActionResolver.Resolve(data.Action);
			}
			else
			{
				Action = new BatchAction(ButtonActionResolver.Resolve(data.Actions));
			}

			Style = data.Style;

			Font = data.Font;
			FontSize = data.FontSize;
			FontWeight = data.FontWeight;

			Visibility = data.Visibility;
		}

		public void Dispose()
		{
			Action?.Dispose();
		}
	}
}
