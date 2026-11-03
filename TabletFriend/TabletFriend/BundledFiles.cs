using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TabletFriend
{
    internal sealed class BundledFiles
    {
        private readonly Dictionary<string, byte[]> _files;
        private readonly Dictionary<string, HashSet<string>> _hashes;

        internal static BundledFiles Current { get; } = Load();

        internal BundledFiles(Dictionary<string, byte[]> files, Dictionary<string, HashSet<string>> hashes = null)
        {
            _files = new Dictionary<string, byte[]>(files, StringComparer.OrdinalIgnoreCase);
            _hashes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (hashes != null)
                foreach (var entry in hashes) _hashes[entry.Key] = new HashSet<string>(entry.Value);
            foreach (var file in files)
            {
                if (!_hashes.TryGetValue(file.Key, out var values))
                    _hashes[file.Key] = values = new HashSet<string>();
                values.Add(Hash(file.Key, file.Value));
            }
        }

        internal bool HasName(string path) => _hashes.ContainsKey(path.Replace('\\', '/'));
        internal byte[] ImageContent(string path)
        {
            path = path.Replace('\\', '/');
            if (path.StartsWith("files/", StringComparison.OrdinalIgnoreCase)) path = path.Substring("files/".Length);
            return path.StartsWith("icons/", StringComparison.OrdinalIgnoreCase) && _files.TryGetValue(path, out var content)
                ? content : null;
        }
        internal bool IsUnchanged(string path, byte[] content) =>
            _hashes.TryGetValue(path.Replace('\\', '/'), out var hashes) && hashes.Contains(Hash(path, content));

        internal DirectoryImport<T> Read<T>(string folder)
        {
            var result = new DirectoryImport<T>("bundled/" + folder);
            foreach (var file in _files.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                if (file.Key.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetExtension(file.Key), ".yaml", StringComparison.OrdinalIgnoreCase))
                    result.Files.Add(Importer.Parse<T>("bundled/" + file.Key, Decode(file.Value)));
            return result;
        }

        internal static string Hash(string path, byte[] content)
        {
            // Git archives use LF while Windows checkouts commonly use CRLF.
            if (string.Equals(Path.GetExtension(path), ".yaml", StringComparison.OrdinalIgnoreCase))
                content = Encoding.UTF8.GetBytes(Decode(content).Replace("\r\n", "\n"));
            return Convert.ToHexString(SHA256.HashData(content));
        }

        private static string Decode(byte[] content)
        {
            using var reader = new StreamReader(new MemoryStream(content), Encoding.UTF8, true);
            return reader.ReadToEnd();
        }

        private static BundledFiles Load()
        {
            var assembly = typeof(BundledFiles).Assembly;
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in assembly.GetManifestResourceNames())
            {
                var path = name.Replace('\\', '/');
                if (!path.StartsWith("Bundled/", StringComparison.Ordinal)) continue;
                using var stream = assembly.GetManifestResourceStream(name);
                using var content = new MemoryStream();
                stream.CopyTo(content);
                files[path.Substring("Bundled/".Length)] = content.ToArray();
            }
            using var hashes = new StreamReader(assembly.GetManifestResourceStream("Migration/bundled-hashes.json"));
            return new BundledFiles(files,
                JsonConvert.DeserializeObject<Dictionary<string, HashSet<string>>>(hashes.ReadToEnd()));
        }
    }
}
