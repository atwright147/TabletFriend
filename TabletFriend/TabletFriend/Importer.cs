using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TabletFriend.Data;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TabletFriend
{
    internal sealed class ImportResult<T>
    {
        public string Path { get; }
        public T Data { get; }
        public string Error { get; }
        public ImportResult(string path, T data, string error = null)
        { Path = path; Data = data; Error = error; }
    }

    internal sealed class DirectoryImport<T>
    {
        public string Path { get; }
        public List<ImportResult<T>> Files { get; } = new List<ImportResult<T>>();
        public string Error { get; set; }
        public DirectoryImport(string path) { Path = path; }
    }

    internal static class Importer
    {
        internal static ImportResult<T> Parse<T>(string path, string text)
        {
            try
            {
                var normalized = text.Replace("\t", "  ");
                if (typeof(T) == typeof(LayoutData)) LayoutSyntaxValidator.Validate(normalized, path);
                var builder = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance);
                // Layouts have a strict, published schema. Keep themes on their
                // existing permissive path until they have their own schema.
                if (typeof(T) != typeof(LayoutData)) builder.IgnoreUnmatchedProperties();
                var deserializer = builder.Build();
                var data = deserializer.Deserialize<T>(normalized);
                if (data == null) throw new FormatException("Empty configuration.");
                if (data is LayoutData layout) LayoutValidator.Validate(layout, path);
                return new ImportResult<T>(path, data);
            }
            catch (Exception e) { return new ImportResult<T>(path, default, e.Message); }
        }

        internal static async Task<ImportResult<T>> ReadAsync<T>(string path, CancellationToken token)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var text = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(text)) return Parse<T>(path, text);
                    if (attempt == 4) return new ImportResult<T>(path, default, "Empty configuration.");
                }
                catch (IOException e)
                {
                    if (attempt == 4) return new ImportResult<T>(path, default, e.Message);
                }
                catch (UnauthorizedAccessException e) { return new ImportResult<T>(path, default, e.Message); }
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            throw new InvalidOperationException("Unreachable read attempt.");
        }

        internal static async Task<DirectoryImport<T>> ReadDirectoryAsync<T>(string path, CancellationToken token)
        {
            var result = new DirectoryImport<T>(path);
            string[] files;
            try { files = Directory.GetFiles(path, AppState.ConfigExtension); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { result.Error = e.Message; return result; }
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var file in files)
                result.Files.Add(await ReadAsync<T>(file, token).ConfigureAwait(false));
            return result;
        }
    }
}
