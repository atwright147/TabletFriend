using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TabletFriend.Data;
using Xunit;

namespace TabletFriend.Tests
{
    public class ConfigurationTests
    {
        [Fact]
        public void RetainsLastValidFileWhilePublishingOtherChangesAndRecovery()
        {
            var cache = new ConfigurationCache<int, string>();
            var errors = new Dictionary<string, string>();
            cache.Apply(Read(new ImportResult<int>("a.yaml", 1), new ImportResult<int>("b.yaml", 2)), Convert, errors);
            cache.Apply(Read(new ImportResult<int>("a.yaml", 0, "invalid"), new ImportResult<int>("b.yaml", 3),
                new ImportResult<int>("new.yaml", 0, "invalid")), Convert, errors);
            Assert.Equal("1", cache.ByName()["a"]);
            Assert.Equal("3", cache.ByName()["b"]);
            Assert.False(cache.ByName().ContainsKey("new"));
            Assert.Equal(2, errors.Count);
            errors.Clear();
            cache.Apply(Read(new ImportResult<int>("a.yaml", 4)), Convert, errors);
            Assert.Equal("4", cache.ByName()["a"]);
            Assert.False(cache.ByName().ContainsKey("b"));
            Assert.Empty(errors);
        }

        [Fact]
        public void ConstructionFailureAndEnumerationFailureKeepPreviousModels()
        {
            var cache = new ConfigurationCache<int, string>();
            var errors = new Dictionary<string, string>();
            cache.Apply(Read(new ImportResult<int>("a.yaml", 1)), Convert, errors);
            cache.Apply(Read(new ImportResult<int>("a.yaml", 2)), _ => throw new FormatException("bad model"), errors);
            Assert.Equal("1", cache.ByName()["a"]);
            cache.Apply(new DirectoryImport<int>("layouts") { Error = "unavailable" }, Convert, errors);
            Assert.Equal("1", cache.ByName()["a"]);
            Assert.Contains("layouts", errors.Keys);
        }

        [Fact]
        public void RenameRemovesOldIdentityAndDeletionCanEmptyCollection()
        {
            var cache = new ConfigurationCache<int, string>();
            var errors = new Dictionary<string, string>();
            cache.Apply(Read(new ImportResult<int>("old.yaml", 1)), Convert, errors);
            cache.Apply(Read(new ImportResult<int>("renamed.yaml", 1)), Convert, errors);
            Assert.False(cache.ByName().ContainsKey("old"));
            Assert.Equal("1", cache.ByName()["renamed"]);
            cache.Apply(Read(), Convert, errors);
            Assert.Empty(cache.ByName());
        }

        [Fact]
        public void SelectionUsesRequestedThenDefaultThenOrdinalThenBuiltin()
        {
            var items = new Dictionary<string, int> { ["z"] = 1, ["a"] = 2, ["default"] = 3 };
            Assert.Equal("z", ConfigurationSelection.Select(items, "z"));
            Assert.Equal("default", ConfigurationSelection.Select(items, "missing"));
            items.Remove("default");
            Assert.Equal("a", ConfigurationSelection.Select(items, "missing"));
            items.Clear();
            Assert.Equal("default", ConfigurationSelection.Select(items, null));
        }

        [Fact]
        public async Task LockedFileRetriesOffCallingThreadAndRecovers()
        {
            using var folder = new TestDirectory();
            var path = Path.Combine(folder.Path, "layout.yaml");
            File.WriteAllText(path, "buttons: {}");
            Task<ImportResult<LayoutData>> read;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                read = Importer.ReadAsync<LayoutData>(path, CancellationToken.None);
                Assert.False(read.IsCompleted);
            }
            Assert.Null((await read.WaitAsync(TimeSpan.FromSeconds(5))).Error);
        }

        [Fact]
        public async Task EmptyFileRetriesAreCancellableAndEnumerationErrorsAreStructured()
        {
            using var folder = new TestDirectory();
            var path = Path.Combine(folder.Path, "empty.yaml");
            File.WriteAllText(path, "");
            using var stop = new CancellationTokenSource();
            var read = Importer.ReadAsync<LayoutData>(path, stop.Token);
            Assert.False(read.IsCompleted);
            stop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
            var missing = await Importer.ReadDirectoryAsync<LayoutData>(Path.Combine(folder.Path, "missing"), CancellationToken.None);
            Assert.NotNull(missing.Error);
        }

        [Fact]
        public void TabsAndUnknownPropertiesRemainSupported()
        {
            var read = Importer.Parse<LayoutData>("tabs.yaml", "unknown: true\nbuttons:\n\tcut:\n\t\taction: ctrl+x\n");
            Assert.Null(read.Error);
            Assert.Contains("cut", read.Data.Buttons.Keys);
        }

        private static string Convert(int value) => value.ToString();
        private static DirectoryImport<int> Read(params ImportResult<int>[] files)
        {
            var read = new DirectoryImport<int>("layouts");
            read.Files.AddRange(files);
            return read;
        }
    }

    internal sealed class TestDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TabletFriend-tests-" + Guid.NewGuid());
        public TestDirectory() { Directory.CreateDirectory(Path); }
        public void Dispose() { Directory.Delete(Path, true); }
    }
}
