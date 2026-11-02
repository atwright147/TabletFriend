using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
using TabletFriend.Data;
using TabletFriend.Models;
using Xunit;

namespace TabletFriend.Tests
{
    public class BuiltInIconTests
    {
        [Theory]
        [InlineData("brush-plus")]
        [InlineData("brush-minus")]
        [InlineData("opacity-plus")]
        [InlineData("opacity-minus")]
        public void NamesResolveWithExistingNormalizationAndIndependentControls(string name)
        {
            Sta.Run(() =>
            {
                using var first = new ButtonModel(new ButtonData { Icon = name });
                var icon = Assert.IsType<BuiltInIcon>(first.Icon);
                Assert.Equal(24, icon.Width);
                Assert.Equal(24, icon.Height);
                foreach (var alias in new[] { name.ToUpperInvariant(), name.Replace('-', '_'), name.Replace("-", "") })
                {
                    using var other = new ButtonModel(new ButtonData { Icon = alias });
                    Assert.IsType<BuiltInIcon>(other.Icon);
                    Assert.NotSame(icon, other.Icon);
                }
            });
        }

        [Theory]
        [InlineData("brush", PackIconKind.Brush)]
        [InlineData("content_copy", PackIconKind.ContentCopy)]
        public void ExistingMaterialIconsRemainAvailable(string name, PackIconKind expected)
        {
            Sta.Run(() =>
            {
                using var model = new ButtonModel(new ButtonData { Icon = name });
                Assert.Equal(expected, Assert.IsType<PackIcon>(model.Icon).Kind);
            });
        }

        [Fact]
        public void UnknownNamesStillFallBackToText()
        {
            Sta.Run(() =>
            {
                using var model = new ButtonModel(new ButtonData { Icon = "not-an-icon", Text = "Fallback" });
                Assert.Null(model.Icon);
                Assert.Equal("Fallback", model.Text);
            });
        }

        [Theory]
        [InlineData("brush-plus", 80, 80)]
        [InlineData("brush-minus", 80, 80)]
        [InlineData("opacity-plus", 21, 21)]
        [InlineData("opacity-minus", 21, 21)]
        public void CompiledVectorsRenderAndFollowParentForegroundChanges(string name, int x, int y)
        {
            Sta.Run(() =>
            {
                using var model = new ButtonModel(new ButtonData { Icon = name });
                var icon = Assert.IsType<BuiltInIcon>(model.Icon);
                icon.Width = icon.Height = 128;
                var parent = new Button { Content = icon, Foreground = Brushes.Red };
                // A logical parent is sufficient for WPF foreground inheritance.
                Assert.Same(parent.Foreground, icon.Foreground);
                Assert.Equal(Colors.Red, Pixel(Render(icon), x, y));
                parent.Foreground = Brushes.Blue;
                Assert.Same(parent.Foreground, icon.Foreground);
                Assert.Equal(Colors.Blue, Pixel(Render(icon), x, y));
                Assert.Equal(0, Pixel(Render(icon), 5, 5).A);
            });
        }

        [Theory]
        [InlineData("brush-plus", 93, 85, 0)]
        [InlineData("brush-minus", 93, 85, 255)]
        [InlineData("brush-plus", 93, 93, 0)]
        [InlineData("brush-minus", 93, 93, 0)]
        [InlineData("opacity-minus", 32, 21, 0)]
        [InlineData("opacity-plus", 32, 21, 255)]
        public void SvgCutoutsAndNestedTransformsArePreserved(string name, int x, int y, int alpha)
        {
            Sta.Run(() =>
            {
                using var model = new ButtonModel(new ButtonData { Icon = name });
                var icon = Assert.IsType<BuiltInIcon>(model.Icon);
                icon.Width = icon.Height = 128;
                Assert.Equal(alpha, Pixel(Render(icon), x, y).A);
            });
        }

        private static RenderTargetBitmap Render(FrameworkElement icon)
        {
            icon.Measure(new Size(128, 128));
            icon.Arrange(new Rect(0, 0, 128, 128));
            icon.UpdateLayout();
            var bitmap = new RenderTargetBitmap(128, 128, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(icon);
            return bitmap;
        }

        private static Color Pixel(BitmapSource bitmap, int x, int y)
        {
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
        }
    }
}
