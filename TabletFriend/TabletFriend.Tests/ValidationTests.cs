using System;
using System.IO;
using System.Numerics;
using TabletFriend.Data;
using TabletFriend.Models;
using Xunit;

namespace TabletFriend.Tests
{
    public class ValidationTests
    {
        [Theory]
        [InlineData("0,1")]
        [InlineData("1,0")]
        [InlineData("-1,2")]
        [InlineData("1,-2")]
        [InlineData("1.5,2")]
        [InlineData("a,2")]
        [InlineData("1")]
        [InlineData("1,")]
        [InlineData("1,2,3")]
        [InlineData("2147483648,1")]
        [InlineData("")]
        public void InvalidSizesAreStructuredErrorsForButtonsAndSpacers(string size)
        {
            foreach (var spacer in new[] { "true", "false" })
            {
                var result = Importer.Parse<LayoutData>("custom.yaml",
                    $"buttons:\n  broken:\n    spacer: {spacer}\n    size: '{size}'\n");
                Assert.Null(result.Data);
                Assert.Contains("custom.yaml", result.Error);
                Assert.Contains("broken", result.Error);
            }
        }

        [Fact]
        public void DefaultAndWhitespaceSizesRemainCompatible()
        {
            Assert.Equal(Vector2.One, ModelConverter.ConvertVector2(null));
            Assert.Equal(new Vector2(2, 3), ModelConverter.ConvertVector2(" 2 , 3 "));
        }

        [Fact]
        public void BundledLayoutsValidate()
        {
            var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "files", "layouts"), "*.yaml");
            Assert.NotEmpty(files);
            foreach (var file in files)
                Assert.Null(Importer.Parse<LayoutData>(file, File.ReadAllText(file)).Error);
        }
    }
}
