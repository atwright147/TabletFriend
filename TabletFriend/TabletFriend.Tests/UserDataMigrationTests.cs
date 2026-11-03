using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace TabletFriend.Tests
{
    public class UserDataMigrationTests
    {
        private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

        [Fact]
        public void BundledCatalogIncludesEmbeddedDefaultsAndHistoricalHashes()
        {
            var catalog = BundledFiles.Current;
            var layouts = catalog.Read<Data.LayoutData>("layouts");
            Assert.Equal(7, layouts.Files.Count);
            Assert.All(layouts.Files, layout => Assert.Null(layout.Error));
            Assert.NotEmpty(catalog.Read<Data.ThemeData>("themes").Files);
            // A removed historical theme must not be mistaken for a personal theme.
            Assert.True(catalog.IsUnchanged("themes/white.yaml", Bytes(
                "rounding: 16\ndefault_style: accent\n\nprimary_color: \"#9ADDF9\"\n" +
                "secondary_color: \"#212121\"\nbackground_color: \"#fff\"\n")));
            foreach (var path in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "files", "layouts")))
                Assert.True(catalog.IsUnchanged("layouts/" + Path.GetFileName(path), File.ReadAllBytes(path)));

            var historical = Bytes("buttons: {}\n# old release\n");
            var fake = new BundledFiles(new Dictionary<string, byte[]>
            { ["layouts/default.yaml"] = Bytes("buttons: {}\n# current\n") },
                new Dictionary<string, HashSet<string>>
                { ["layouts/default.yaml"] = new HashSet<string> { BundledFiles.Hash("old.yaml", historical) } });
            Assert.True(fake.IsUnchanged("layouts/default.yaml", Bytes("buttons: {}\r\n# old release\r\n")));
            Assert.False(fake.IsUnchanged("layouts/default.yaml", Bytes("buttons: {}\n# user edit\n")));
            Assert.False(fake.HasName("layouts/personal.yaml"));
        }

        [Fact]
        public void ImportsCustomFilesSkipsStockAndPreservesUncertainNamesForReview()
        {
            using var folder = new TestDirectory();
            var source = Path.Combine(folder.Path, "old", "files");
            var root = Path.Combine(folder.Path, "Documents", "TabletFriend");
            var stock = Bytes("buttons: {}\n");
            var catalog = new BundledFiles(new Dictionary<string, byte[]>
            {
                ["layouts/default.yaml"] = stock,
                ["layouts/edited.yaml"] = stock,
                ["themes/default.yaml"] = Bytes("rounding: 8\n")
            });
            Write(source, "layouts/default.yaml", "buttons: {}\r\n");
            Write(source, "layouts/edited.yaml", "buttons: {}\n# personal edit");
            Write(source, "layouts/rebelle.yaml", "buttons: {}\n# custom");
            Write(source, "themes/default.yaml", "rounding: 8\n");
            Write(source, "themes/personal.yaml", "rounding: 12\n");
            Write(source, "icons/custom.svg", "<svg/>");
            var result = UserDataMigration.Run(root, new[] { source }, catalog);
            Assert.Empty(result.Errors);
            Assert.True(result.FirstRun);
            Assert.Equal(3, result.Imported);
            Assert.Equal(1, result.Review);
            Assert.True(File.Exists(Path.Combine(root, "layouts/rebelle.yaml")));
            Assert.True(File.Exists(Path.Combine(root, "themes/personal.yaml")));
            Assert.True(File.Exists(Path.Combine(root, "icons/custom.svg")));
            Assert.False(File.Exists(Path.Combine(root, "layouts/default.yaml")));
            Assert.False(File.Exists(Path.Combine(root, "layouts/edited.yaml")));
            var review = Assert.Single(Directory.GetFiles(Path.Combine(root, "Imports"), "edited.yaml", SearchOption.AllDirectories));
            Assert.Equal(File.ReadAllText(Path.Combine(source, "layouts/edited.yaml")), File.ReadAllText(review));
            Assert.Contains(source, File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(review)), "migration-report.txt")));
            // A deletion in the new folder is intentional and must survive later startups.
            File.Delete(Path.Combine(root, "layouts/rebelle.yaml"));
            var repeated = UserDataMigration.Run(root, new[] { source }, catalog);
            Assert.False(repeated.FirstRun);
            Assert.Equal(0, repeated.Imported + repeated.Review);
            Assert.Null(repeated.Notice(root));
            Assert.False(File.Exists(Path.Combine(root, "layouts/rebelle.yaml")));
            Assert.True(File.Exists(Path.Combine(source, "layouts/rebelle.yaml")));
        }

        [Fact]
        public void ConflictsNeverOverwriteUserFilesAndPreserveBothLegacyVersions()
        {
            using var folder = new TestDirectory();
            var root = Path.Combine(folder.Path, "custom");
            var source = Path.Combine(folder.Path, "files");
            var backup = Path.Combine(folder.Path, "files.backup");
            Write(root, "layouts/rebelle.yaml", "new user edit");
            Write(source, "layouts/rebelle.yaml", "legacy version");
            Write(backup, "layouts/rebelle.yaml", "older version");
            var result = UserDataMigration.Run(root, new[] { source, backup }, EmptyCatalog());
            Assert.Empty(result.Errors);
            Assert.Equal(2, result.Review);
            Assert.Equal("new user edit", File.ReadAllText(Path.Combine(root, "layouts/rebelle.yaml")));
            var preserved = Directory.GetFiles(Path.Combine(root, "Imports"), "rebelle.yaml", SearchOption.AllDirectories)
                .Select(File.ReadAllText).ToArray();
            Assert.Contains("legacy version", preserved);
            Assert.Contains("older version", preserved);
        }

        [Fact]
        public void ConflictingIconsKeepDependentLayoutsTogetherForReview()
        {
            using var folder = new TestDirectory();
            var root = Path.Combine(folder.Path, "custom");
            var source = Path.Combine(folder.Path, "files");
            Write(root, "icons/brush.svg", "new icon");
            Write(source, "icons/brush.svg", "old icon");
            Write(source, "layouts/rebelle.yaml", "buttons: {}\n");
            Write(source, "themes/personal.yaml", "rounding: 12\n");
            var result = UserDataMigration.Run(root, new[] { source }, EmptyCatalog());
            Assert.Empty(result.Errors);
            Assert.Equal(1, result.Imported);
            Assert.Equal(2, result.Review);
            Assert.False(File.Exists(Path.Combine(root, "layouts/rebelle.yaml")));
            Assert.Equal("new icon", File.ReadAllText(Path.Combine(root, "icons/brush.svg")));
            Assert.True(File.Exists(Path.Combine(root, "themes/personal.yaml")));
            Assert.Single(Directory.GetFiles(Path.Combine(root, "Imports"), "brush.svg", SearchOption.AllDirectories));
        }

        [Fact]
        public void FailedMigrationRetriesAndPreviouslyAbsentSourcesAreDiscoveredLater()
        {
            using var folder = new TestDirectory();
            var root = Path.Combine(folder.Path, "custom");
            var source = Path.Combine(folder.Path, "files");
            var later = Path.Combine(folder.Path, "files.backup");
            Write(source, "layouts/personal.yaml", "buttons: {}\n");
            using (var locked = new FileStream(Path.Combine(source, "layouts/personal.yaml"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.NotEmpty(UserDataMigration.Run(root, new[] { source, later }, EmptyCatalog()).Errors);
            Write(later, "layouts/second.yaml", "buttons: {}\n");
            var result = UserDataMigration.Run(root, new[] { source, later }, EmptyCatalog());
            Assert.Empty(result.Errors);
            Assert.Equal(2, result.Imported);
            Assert.True(File.Exists(Path.Combine(root, "layouts/personal.yaml")));
            Assert.True(File.Exists(Path.Combine(root, "layouts/second.yaml")));
        }

        [Fact]
        public void InstallerKeepsPreviousBackupsAndMigrationDiscoversBothInstallLocations()
        {
            using var folder = new TestDirectory();
            var installed = Path.Combine(folder.Path, "installed");
            var portable = Path.Combine(folder.Path, "portable");
            Write(installed, "files/layouts/personal.yaml", "original");
            var first = Installer.BackupLegacyFiles(installed);
            Write(installed, "files/layouts/personal.yaml", "edited");
            var second = Installer.BackupLegacyFiles(installed);
            Assert.NotEqual(first, second);
            Assert.Equal("original", File.ReadAllText(Path.Combine(first, "layouts/personal.yaml")));
            Assert.Equal("edited", File.ReadAllText(Path.Combine(second, "layouts/personal.yaml")));
            Directory.CreateDirectory(portable);
            var sources = UserDataMigration.LegacyRoots(portable, installed).ToArray();
            Assert.Contains(Path.Combine(portable, "files"), sources);
            Assert.Contains(Path.Combine(installed, "files"), sources);
            Assert.Contains(first, sources);
            Assert.Contains(second, sources);
        }

        private static BundledFiles EmptyCatalog() => new BundledFiles(new Dictionary<string, byte[]>());

        private static void Write(string root, string name, string text)
        {
            var path = Path.Combine(root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }
    }
}
