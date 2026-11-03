using System;
using System.Collections.Generic;
using System.IO;
using TabletFriend.Models;

namespace TabletFriend
{
	public static class AppState
	{
		public static readonly string CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

		public static readonly string BundledFilesRoot = Path.Combine(CurrentDirectory, "files");
		public static readonly string FilesRoot = GetFilesRoot();
		public static readonly string LayoutsRoot = Path.Combine(FilesRoot, "layouts");
		public static readonly string ThemesRoot = Path.Combine(FilesRoot, "themes");

		private static string GetFilesRoot()
		{
			var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
				Environment.SpecialFolderOption.DoNotVerify);
			if (string.IsNullOrEmpty(documents))
				documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");
			return Path.Combine(documents, "TabletFriend");
		}

		public const string ConfigExtension = "*.yaml";

		public static Dictionary<string, LayoutModel> Layouts;
		public static Dictionary<string, ThemeModel> Themes;

		public static LayoutModel CurrentLayout;
		public static string CurrentLayoutName;

		/// <summary>
		/// Keeps track of which layout was set by user and not autoswitched to.
		/// </summary>
		public static string LastManuallySetLayout;

		public static ThemeModel CurrentTheme;
		public static string CurrentThemeName;

		public static Settings Settings;
	}
}
