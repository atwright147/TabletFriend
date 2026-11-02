using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WindowsInput.Events;
using Xunit;

namespace TabletFriend.Tests
{
    public class InputStateTests
    {
        [Fact]
        public async Task HoldsTogglesAndReleasesShareOwnership()
        {
            var output = new FakeKeyboard();
            var input = new InputStateService(output);
            await input.HoldAsync(KeyCode.LControl);
            Assert.True(input.IsHeld(KeyCode.LControl));
            await input.ToggleAsync(KeyCode.LControl);
            Assert.False(input.IsHeld(KeyCode.LControl));
            await input.ToggleAsync(KeyCode.LShift);
            await input.ReleaseAsync(KeyCode.LShift);
            Assert.False(input.IsHeld(KeyCode.LShift));
            await input.ReleaseAllAsync();
            Assert.Equal(4, output.Events.Count);
        }

        [Fact]
        public async Task FailedHoldRemainsOwnedAndSuppressionIsReset()
        {
            var output = new FakeKeyboard { FailHold = true };
            var input = new InputStateService(output);
            await Assert.ThrowsAsync<InvalidOperationException>(() => input.HoldAsync(KeyCode.LControl));
            Assert.True(input.IsHeld(KeyCode.LControl));
            Assert.False(input.SimulationInProgress);
            await input.ReleaseAllAsync();
            Assert.False(input.IsHeld(KeyCode.LControl));
            Assert.Equal("up:LControl", output.Events.Last());
        }

        [Fact]
        public async Task PhysicalKeyUpReconcilesOwnership()
        {
            var input = new InputStateService(new FakeKeyboard());
            await input.HoldAsync(KeyCode.LControl);
            input.ObserveKeyEvent(KeyCode.LControl, false, false);
            input.ObserveKeyEvent(KeyCode.LControl, true, true);
            Assert.True(input.IsHeld(KeyCode.LControl));
            input.ObservePhysicalKeyUp(KeyCode.LControl);
            Assert.False(input.IsHeld(KeyCode.LControl));
        }

        [Fact]
        public async Task ShutdownWaitsForInFlightHoldAndRejectsFurtherHolds()
        {
            var output = new FakeKeyboard { HoldGate = NewSource() };
            var input = new InputStateService(output);
            var first = input.HoldAsync(KeyCode.LControl);
            var second = input.ToggleAsync(KeyCode.LShift);
            var shutdown = input.ReleaseAllAsync();
            Assert.False(shutdown.IsCompleted);
            output.HoldGate.SetResult(true);
            await Task.WhenAll(first, second, shutdown).WaitAsync(TimeSpan.FromSeconds(5));
            await input.HoldAsync(KeyCode.LAlt);
            await input.ReleaseAllAsync();
            Assert.Equal(new[] { "down:LControl", "up:LControl" }, output.Events);
            Assert.False(input.IsHeld(KeyCode.LControl));
        }

        [Fact]
        public async Task OverlappingTogglesAreSerialized()
        {
            var output = new FakeKeyboard { HoldGate = NewSource() };
            var input = new InputStateService(output);
            var first = input.ToggleAsync(KeyCode.LControl);
            var second = input.ToggleAsync(KeyCode.LControl);
            output.HoldGate.SetResult(true);
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { "down:LControl", "up:LControl" }, output.Events);
            Assert.False(input.IsHeld(KeyCode.LControl));
        }

        [Fact]
        public async Task ReleaseFailureDoesNotSkipOtherKeysAndShutdownIsIdempotent()
        {
            var output = new FakeKeyboard { FailRelease = KeyCode.LControl };
            var input = new InputStateService(output);
            await input.HoldAsync(KeyCode.LControl, KeyCode.LShift);
            var shutdown = input.ReleaseAllAsync();
            await Assert.ThrowsAsync<AggregateException>(() => shutdown);
            Assert.Same(shutdown, input.ReleaseAllAsync());
            Assert.Contains("up:LControl", output.Events);
            Assert.Contains("up:LShift", output.Events);
            Assert.False(input.IsHeld(KeyCode.LShift));
            Assert.False(input.SimulationInProgress);
        }

        private static TaskCompletionSource<bool> NewSource() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private sealed class FakeKeyboard : IKeyboardOutput
        {
            public List<string> Events { get; } = new List<string>();
            public bool FailHold;
            public KeyCode? FailRelease;
            public TaskCompletionSource<bool> HoldGate;
            public async Task HoldAsync(KeyCode[] keys)
            {
                foreach (var key in keys) Events.Add("down:" + key);
                if (HoldGate != null) await HoldGate.Task;
                if (FailHold) throw new InvalidOperationException("Hold failed after partial injection");
            }
            public Task ReleaseAsync(KeyCode[] keys)
            {
                foreach (var key in keys)
                {
                    Events.Add("up:" + key);
                    if (key == FailRelease) throw new InvalidOperationException("Release failed");
                }
                return Task.CompletedTask;
            }
        }
    }
}
