using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TabletFriend.Data;
using TabletFriend.Models;
using Xunit;

namespace TabletFriend.Tests
{
    public class CustomIconTests
    {
        [Fact]
        public void BundledExamplesLoadEmbeddedImagesInsteadOfCustomOverrides()
        {
            Sta.Run(() =>
            {
                using var folder = new TestDirectory();
                Directory.CreateDirectory(Path.Combine(folder.Path, "icons"));
                var path = Path.Combine(folder.Path, "icons", "kot.png");
                WritePng(path, Colors.Red);
                using var personal = new ButtonModel(new ButtonData { Icon = "files/icons/kot.png" }, folder.Path);
                using var bundled = new ButtonModel(new ButtonData { Icon = "files/icons/kot.png" }, folder.Path, bundledIcons: true);
                var personalSource = Assert.IsType<BitmapImage>(Assert.IsType<Image>(personal.Icon).Source);
                var bundledSource = Assert.IsType<BitmapImage>(Assert.IsType<Image>(bundled.Icon).Source);
                Assert.Equal(1, personalSource.PixelWidth);
                Assert.Equal(Colors.Red, Pixel(personalSource, 0, 0));
                Assert.True(bundledSource.PixelWidth > 1);
                Assert.True(bundledSource.IsFrozen);
            });
        }

        [Fact]
        public void CustomPathsSupportMigratedPrefixesAndLegacyFallbackWithoutChangingBundledIcons()
        {
            using var folder = new TestDirectory();
            var user = Directory.CreateDirectory(Path.Combine(folder.Path, "custom")).FullName;
            var app = Directory.CreateDirectory(Path.Combine(folder.Path, "application")).FullName;
            Directory.CreateDirectory(Path.Combine(user, "icons"));
            Directory.CreateDirectory(Path.Combine(app, "files", "icons"));
            var personal = Path.Combine(user, "icons", "personal.svg");
            var legacy = Path.Combine(app, "files", "icons", "legacy.png");
            File.WriteAllText(personal, "icon");
            File.WriteAllText(legacy, "icon");
            Assert.Equal(personal, Path.GetFullPath(CustomIcon.ResolvePath("icons/personal.svg", user, app)));
            Assert.Equal(personal, Path.GetFullPath(CustomIcon.ResolvePath("files/icons/personal.svg", user, app)));
            Assert.Equal(legacy, Path.GetFullPath(CustomIcon.ResolvePath("files/icons/legacy.png", user, app)));
            Assert.Equal(personal, CustomIcon.ResolvePath(personal, user, app));
            Assert.Equal(Path.Combine(app, "icons/personal.svg"), CustomIcon.ResolvePath("icons/personal.svg", null, app));
        }

        private const string Svg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 50">
              <g transform="translate(10 10)">
                <path fill="#ff0000" fill-rule="evenodd" d="M0 0H30V30H0Z M10 10H20V20H10Z"/>
                <circle cx="60" cy="15" r="10" fill="#0000ff"/>
              </g>
            </svg>
            """;

        [Theory]
        [InlineData(".svg")]
        [InlineData(".SVG")]
        public void SvgPreservesColoursTransformsCutoutsAndViewbox(string extension)
        {
            Sta.Run(() =>
            {
                using var folder = new TestDirectory();
                var path = Path.Combine(folder.Path, "custom icon" + extension);
                File.WriteAllText(path, Svg);
                using var model = new ButtonModel(new ButtonData { Icon = path, IconStretch = StretchData.Fit });
                var icon = Assert.IsType<Image>(model.Icon);
                Assert.IsType<DrawingImage>(icon.Source);
                Assert.Equal(100, icon.Source.Width);
                Assert.Equal(50, icon.Source.Height);
                var bitmap = Render(icon, 200, 100);
                Assert.Equal(Colors.Red, Pixel(bitmap, 30, 30));
                Assert.Equal(Colors.Blue, Pixel(bitmap, 140, 50));
                Assert.Equal(0, Pixel(bitmap, 50, 50).A);
                Assert.Equal(0, Pixel(bitmap, 5, 5).A);
                // Loading must not keep the source file locked or reuse stale SVG contents.
                File.WriteAllText(path, Svg.Replace("#ff0000", "#00ff00"));
                using var updated = new ButtonModel(new ButtonData { Icon = path, IconStretch = StretchData.Fit });
                Assert.Equal(Colors.Lime, Pixel(Render(Assert.IsType<Image>(updated.Icon), 200, 100), 30, 30));
            });
        }

        [Theory]
        [InlineData(".svg", StretchData.None)]
        [InlineData(".svg", StretchData.Fit)]
        [InlineData(".svg", StretchData.Fill)]
        [InlineData(".png", StretchData.None)]
        [InlineData(".png", StretchData.Fit)]
        [InlineData(".png", StretchData.Fill)]
        public void RelativePathsAndStretchWorkForBothFormats(string extension, StretchData stretch)
        {
            Sta.Run(() =>
            {
                var name = "custom-icon-" + Guid.NewGuid() + extension;
                var path = Path.Combine(AppState.CurrentDirectory, name);
                try
                {
                    if (extension == ".svg") File.WriteAllText(path, Svg);
                    else
                    {
                        WritePng(path, Colors.Red);
                    }
                    using var model = new ButtonModel(new ButtonData { Icon = name, IconStretch = stretch });
                    var icon = Assert.IsType<Image>(model.Icon);
                    Assert.Equal((Stretch)stretch, icon.Stretch);
                    if (extension == ".png")
                    {
                        Assert.Equal(Colors.Red, Pixel(Assert.IsAssignableFrom<BitmapSource>(icon.Source), 0, 0));
                        WritePng(path, Colors.Blue);
                        using var updated = new ButtonModel(new ButtonData { Icon = name });
                        var updatedIcon = Assert.IsType<Image>(updated.Icon);
                        Assert.Equal(Colors.Blue, Pixel(Assert.IsAssignableFrom<BitmapSource>(updatedIcon.Source), 0, 0));
                    }
                    else Assert.IsType<DrawingImage>(icon.Source);
                }
                finally { File.Delete(path); }
            });
        }

        [Fact]
        public void InvalidSvgRetainsPreviousModelAndReportsItsPath()
        {
            Sta.Run(() =>
            {
                using var folder = new TestDirectory();
                var path = Path.Combine(folder.Path, "icon.svg");
                File.WriteAllText(path, Svg);
                var cache = new ConfigurationCache<ButtonData, ButtonModel>();
                var read = new DirectoryImport<ButtonData>(folder.Path);
                read.Files.Add(new ImportResult<ButtonData>("layout.yaml", new ButtonData { Icon = path }));
                var errors = new Dictionary<string, string>();
                cache.Apply(read, data => new ButtonModel(data), errors);
                using var previous = cache.ByName()["layout"];
                File.WriteAllText(path, "<svg><broken");
                cache.Apply(read, data => new ButtonModel(data), errors);
                Assert.Same(previous, cache.ByName()["layout"]);
                Assert.Contains(path, errors["layout.yaml"]);
                File.WriteAllText(path, Svg);
                errors.Clear();
                cache.Apply(read, data => new ButtonModel(data), errors);
                using var recovered = cache.ByName()["layout"];
                Assert.NotSame(previous, recovered);
                Assert.Empty(errors);
            });
        }

        private static RenderTargetBitmap Render(Image icon, int width, int height)
        {
            icon.Measure(new Size(width, height));
            icon.Arrange(new Rect(0, 0, width, height));
            icon.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(icon);
            return bitmap;
        }

        private static void WritePng(string path, Color colour)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 96, 96,
                PixelFormats.Bgra32, null, new byte[] { colour.B, colour.G, colour.R, colour.A }, 4)));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        private static Color Pixel(BitmapSource bitmap, int x, int y)
        {
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
        }
    }
}
