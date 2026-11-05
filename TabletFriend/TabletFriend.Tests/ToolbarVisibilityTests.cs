using System;
using TabletFriend.TabletMode;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TabletFriend.Tests
{
    public class ToolbarVisibilityTests
    {
        [Theory]
        [InlineData("layout: default", true)]
        [InlineData("toolbar_autohide_enabled: false", false)]
        [InlineData("toolbar_autohide_enabled: true", true)]
        public void AutohideDefaultsToEnabledAndExplicitPreferencesStillLoad(string yaml, bool expected)
        {
            var settings = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build().Deserialize<Settings>(yaml);
            Assert.Equal(expected, settings.ToolbarAutohideEnabled);
        }

        [Fact]
        public void DisabledAutohideShowsToolbarAndThenPreservesManualVisibilityAcrossPostureChanges()
        {
            Sta.Run(() =>
            {
                var previousSettings = AppState.Settings;
                var settings = new Settings { ToolbarAutohideEnabled = false };
                AppState.Settings = settings;
                var visible = false;
                var notifications = 0;
                Action<object[]> show = _ => { visible = true; ++notifications; };
                Action<object[]> hide = _ => { visible = false; ++notifications; };
                EventBeacon.Subscribe(Events.Maximize, show);
                EventBeacon.Subscribe(Events.Minimize, hide);
                try
                {
                    ToolbarAutohider.ApplySettings();
                    Assert.True(visible);
                    Assert.Equal(1, notifications);
                    foreach (var tabletMode in new[] { false, true, false, true })
                        ToolbarAutohider.UpdateVisibility(tabletMode);
                    Assert.True(visible);
                    Assert.Equal(1, notifications);

                    // A manual hide stays in effect even if a timer tick was already queued.
                    EventBeacon.SendEvent(Events.Minimize);
                    ToolbarAutohider.UpdateVisibility(false);
                    ToolbarAutohider.UpdateVisibility(true);
                    Assert.False(visible);
                    Assert.Equal(2, notifications);

                    settings.ToolbarAutohideEnabled = true;
                    ToolbarAutohider.ApplySettings();
                    ToolbarAutohider.UpdateVisibility(true);
                    Assert.True(visible);
                    ToolbarAutohider.UpdateVisibility(false);
                    Assert.False(visible);

                    settings.ToolbarAutohideEnabled = false;
                    ToolbarAutohider.ApplySettings();
                    Assert.True(visible);
                    var countAfterDisable = notifications;
                    ToolbarAutohider.UpdateVisibility(false);
                    ToolbarAutohider.UpdateVisibility(true);
                    Assert.True(visible);
                    Assert.Equal(countAfterDisable, notifications);
                }
                finally
                {
                    ToolbarAutohider.StopWatching();
                    EventBeacon.Unsubscribe(Events.Maximize, show);
                    EventBeacon.Unsubscribe(Events.Minimize, hide);
                    AppState.Settings = previousSettings;
                }
            });
        }
    }
}
