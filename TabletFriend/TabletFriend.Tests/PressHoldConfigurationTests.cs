using TabletFriend.Actions;
using TabletFriend.Data;
using TabletFriend.Models;
using Xunit;

namespace TabletFriend.Tests
{
    public class PressHoldConfigurationTests
    {
        [Fact]
        public void ExistingButtonsKeepTheirDefaults()
        {
            var data = Parse("action: toggle ctrl").Data.Buttons["control"];
            using var button = new ButtonModel(data);
            Assert.False(button.PressAndHold);
            Assert.False(button.QuickPressToggle);
            Assert.Equal(250, button.QuickPressThresholdMs);
            Assert.IsType<ToggleAction>(button.Action);
        }

        [Theory]
        [InlineData("ctrl")]
        [InlineData("ctrl+shift")]
        public void OptionsFlowFromYamlToModel(string action)
        {
            var result = Parse($"action: {action}\npress_and_hold: true\nquick_press_toggle: true\nquick_press_threshold_ms: 400");
            Assert.Null(result.Error);
            using var model = new LayoutModel(result.Data);
            var button = Assert.Single(model.Buttons);
            Assert.True(button.PressAndHold);
            Assert.True(button.QuickPressToggle);
            Assert.Equal(400, button.QuickPressThresholdMs);
            Assert.IsType<KeyAction>(button.Action);
        }

        [Theory]
        [InlineData("toggle ctrl")]
        [InlineData("hold ctrl")]
        [InlineData("release ctrl")]
        [InlineData("repeat ctrl")]
        [InlineData("type hello")]
        [InlineData("wait 1")]
        [InlineData("cmd app.exe")]
        [InlineData("layout default")]
        [InlineData("hide")]
        [InlineData("dock left")]
        [InlineData("undock")]
        [InlineData("")]
        public void PrefixedAndMissingActionsAreRejected(string action)
        {
            AssertError($"action: '{action}'\npress_and_hold: true", "plain key action");
        }

        [Theory]
        [InlineData("action: ctrl\nquick_press_toggle: true", "requires press_and_hold")]
        [InlineData("action: ctrl\nquick_press_threshold_ms: 0", "must be positive")]
        [InlineData("action: ctrl\nquick_press_threshold_ms: -1", "must be positive")]
        [InlineData("actions: [ctrl, shift]\npress_and_hold: true", "actions batch")]
        public void InvalidOptionsHaveButtonAndFileContext(string yaml, string error) => AssertError(yaml, error);

        [Fact]
        public void SpacersIgnoreInvalidActionSettings()
        {
            var result = Parse("spacer: true\naction: not-a-key\npress_and_hold: true\nquick_press_toggle: true\nquick_press_threshold_ms: -1");
            Assert.Null(result.Error);
            using var model = new LayoutModel(result.Data);
            Assert.Null(Assert.Single(model.Buttons).Action);
        }

        private static ImportResult<LayoutData> Parse(string yaml) =>
            Importer.Parse<LayoutData>("hold.yaml", "buttons:\n  control:\n    " + yaml.Replace("\n", "\n    ") + "\n");

        private static void AssertError(string yaml, string error)
        {
            var result = Parse(yaml);
            Assert.Null(result.Data);
            Assert.Contains("hold.yaml", result.Error);
            Assert.Contains("control", result.Error);
            Assert.Contains(error, result.Error);
        }
    }
}
