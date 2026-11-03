using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TabletFriend
{
    internal sealed class MigrationResult
    {
        public int Imported;
        public int Review;
        public bool FirstRun;
        public List<string> Errors { get; } = new List<string>();

        public string Notice(string root) => FirstRun || Imported > 0 || Review > 0
            ? $"Custom layouts, themes and icons now belong in '{root}'. " +
              $"Imported {Imported} files. " +
              (Review > 0 ? $"{Review} files need review in '{Path.Combine(root, "Imports")}'. " : "") +
              "Original files were preserved. Use settings > open custom files directory to find them."
            : null;
    }

    internal static class UserDataMigration
    {
        internal static IEnumerable<string> LegacyRoots(string applicationDirectory, string installedDirectory)
        {
            foreach (var directory in new[] { applicationDirectory, installedDirectory }
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return Path.Combine(directory, "files");
                if (!Directory.Exists(directory)) continue;
                foreach (var backup in Directory.GetDirectories(directory, "files.backup*").OrderBy(p => p, StringComparer.Ordinal))
                    yield return backup;
            }
        }

        internal static MigrationResult Run(string root, IEnumerable<string> sources, BundledFiles bundled)
        {
            var result = new MigrationResult();
            try
            {
                foreach (var folder in new[] { "layouts", "themes", "icons" })
                    Directory.CreateDirectory(Path.Combine(root, folder));
                var statePath = Path.Combine(root, ".migration-v1.json");
                result.FirstRun = !File.Exists(statePath);
                var completed = result.FirstRun ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(JsonConvert.DeserializeObject<string[]>(File.ReadAllText(statePath))
                        ?? throw new InvalidDataException("The migration record is empty."), StringComparer.OrdinalIgnoreCase);
                foreach (var source in sources.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!Directory.Exists(source) || completed.Contains(source)
                        || string.Equals(Path.GetFullPath(root), source, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        ImportSource(root, source, bundled, result);
                        completed.Add(source);
                        SaveState(statePath, completed);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    { result.Errors.Add($"Could not migrate '{source}': {e.Message}. Originals were preserved; migration will retry on restart."); }
                }
                SaveState(statePath, completed);
            }
            catch (Exception e)
            { result.Errors.Add($"Could not prepare custom files in '{root}': {e.Message}"); }
            return result;
        }

        private static void SaveState(string path, HashSet<string> sources)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(sources.OrderBy(p => p), Formatting.Indented));
                File.Move(temporary, path, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static void ImportSource(string root, string source, BundledFiles bundled, MigrationResult result)
        {
            var files = new List<(string Name, byte[] Content)>();
            foreach (var folder in new[] { "icons", "layouts", "themes" })
            {
                var directory = Path.Combine(source, folder);
                if (!Directory.Exists(directory)) continue;
                // Do not follow junctions or symbolic links out of a legacy files directory.
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = folder == "icons", IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };
                foreach (var path in Directory.EnumerateFiles(directory, "*", options).OrderBy(p => p, StringComparer.Ordinal))
                {
                    if (folder != "icons" && !string.Equals(Path.GetExtension(path), ".yaml", StringComparison.OrdinalIgnoreCase)) continue;
                    files.Add((Path.GetRelativePath(source, path).Replace('\\', '/'), File.ReadAllBytes(path)));
                }
            }

            bool NeedsReview(string name, byte[] content) => bundled.HasName(name)
                || (File.Exists(Path.Combine(root, name)) && !Same(Path.Combine(root, name), content));
            // A layout must not silently pick up a different image when an asset collides.
            var assetConflict = files.Any(file => file.Name.StartsWith("icons/", StringComparison.OrdinalIgnoreCase)
                && !bundled.IsUnchanged(file.Name, file.Content) && NeedsReview(file.Name, file.Content));
            var label = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (label.Length > 64) label = label.Substring(0, 64);
            var reviewRoot = Path.Combine(root, "Imports", label + "-" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(source.ToUpperInvariant()))).Substring(0, 16));
            var review = new List<string>();
            foreach (var file in files)
            {
                if (bundled.IsUnchanged(file.Name, file.Content)) continue;
                var target = Path.Combine(root, file.Name);
                if (File.Exists(target) && Same(target, file.Content)) continue;
                if (NeedsReview(file.Name, file.Content)
                    || (assetConflict && !file.Name.StartsWith("themes/", StringComparison.OrdinalIgnoreCase)))
                {
                    if (CopyPreserving(Path.Combine(reviewRoot, file.Name), file.Content)) result.Review++;
                    review.Add($"{file.Name}: uncertain bundled filename or conflicting file/asset; review before using.");
                }
                else if (CopyPreserving(target, file.Content)) result.Imported++;
            }
            if (review.Count > 0)
            {
                // Preserve the source's images with review copies, including bundled dependencies.
                foreach (var file in files.Where(file => file.Name.StartsWith("icons/", StringComparison.OrdinalIgnoreCase)))
                    CopyPreserving(Path.Combine(reviewRoot, file.Name), file.Content);
                Directory.CreateDirectory(reviewRoot);
                File.WriteAllText(Path.Combine(reviewRoot, "migration-report.txt"),
                    $"Original source: {source}{Environment.NewLine}" +
                    "Copy reviewed layouts/themes to the corresponding custom folder to enable them. " +
                    "Copy required icons to icons and update paths if names conflict. Originals are unchanged." +
                    Environment.NewLine + string.Join(Environment.NewLine, review));
            }
        }

        private static bool Same(string path, byte[] content) => File.ReadAllBytes(path).SequenceEqual(content);

        private static bool CopyPreserving(string path, byte[] content)
        {
            if (File.Exists(path))
            {
                if (Same(path, content)) return false;
                path = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "-" +
                    Convert.ToHexString(SHA256.HashData(content)).Substring(0, 12) + Path.GetExtension(path));
                if (File.Exists(path))
                {
                    if (Same(path, content)) return false;
                    throw new IOException($"Cannot preserve conflicting file '{path}'.");
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Publish a complete copy without overwriting user edits, even across instances.
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, content);
                File.Move(temporary, path, false);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return true;
        }
    }
}
