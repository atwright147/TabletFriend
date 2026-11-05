using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WindowsInput.Events;
using Xunit;

namespace TabletFriend.Tests
{
    public class PressHoldTests
    {
        [Theory]
        [InlineData(false, 20, false)]
        [InlineData(false, 400, false)]
        [InlineData(true, 249, true)]
        [InlineData(true, 250, false)]
        [InlineData(true, 251, false)]
        public async Task PressHoldsImmediatelyAndReleaseUsesThreshold(bool toggle, int duration, bool locked)
        {
            var output = new HoldKeyboard();
            var input = new InputStateService(output);
            var now = TimeSpan.Zero;
            var button = new PressHoldController(input, new[] { KeyCode.LControl }, toggle, 250, () => now);
            await button.BeginAsync();
            Assert.True(input.IsHeld(KeyCode.LControl));
            Assert.Equal(new[] { ("down", KeyCode.LControl) }, output.Events);
            now += TimeSpan.FromMilliseconds(duration);
            await button.EndAsync(true);
            Assert.Equal(locked, input.IsHeld(KeyCode.LControl));
            await button.CancelAsync();
            Assert.False(input.IsHeld(KeyCode.LControl));
        }

        [Theory]
        [InlineData(50)]
        [InlineData(500)]
        public async Task PressOnLockedKeyKeepsItDownUntilLiftAndThenReleases(int duration)
        {
            var output = new HoldKeyboard();
            var input = new InputStateService(output);
            var now = TimeSpan.Zero;
            var button = new PressHoldController(input, new[] { KeyCode.LShift }, true, 250, () => now);
            await button.BeginAsync();
            await button.EndAsync(true);
            await button.BeginAsync();
            Assert.Single(output.Events);
            Assert.True(input.IsHeld(KeyCode.LShift));
            now += TimeSpan.FromMilliseconds(duration);
            await button.EndAsync(true);
            Assert.False(input.IsHeld(KeyCode.LShift));
            Assert.Equal(new[] { ("down", KeyCode.LShift), ("up", KeyCode.LShift) }, output.Events);
        }

        [Fact]
        public async Task OutsideReleaseAndCancellationNeverLockAndAreIdempotent()
        {
            var output = new HoldKeyboard();
            var input = new InputStateService(output);
            var button = Create(input);
            await button.BeginAsync();
            await button.BeginAsync();
            await button.EndAsync(false);
            await button.EndAsync(true);
            Assert.Equal(2, output.Events.Count);
            await button.BeginAsync();
            await button.CancelAsync();
            await button.EndAsync(true);
            await button.CancelAsync();
            Assert.Equal(4, output.Events.Count);
            Assert.False(button.IsActive);
        }

        [Fact]
        public async Task OverlappingChordsOnlyReleaseTheirOwnContributionInReverseOrder()
        {
            var output = new HoldKeyboard();
            var input = new InputStateService(output);
            var first = new PressHoldController(input, new[] { KeyCode.LControl, KeyCode.LShift }, false, 250);
            var second = new PressHoldController(input, new[] { KeyCode.LShift, KeyCode.LAlt }, false, 250);
            await first.BeginAsync();
            await second.BeginAsync();
            await first.EndAsync(true);
            Assert.False(input.IsHeld(KeyCode.LControl));
            Assert.True(input.IsHeld(KeyCode.LShift));
            Assert.True(second.IsActive);
            await second.EndAsync(true);
            Assert.Equal(new[] { ("down", KeyCode.LControl), ("down", KeyCode.LShift),
                ("down", KeyCode.LAlt), ("up", KeyCode.LControl), ("up", KeyCode.LAlt), ("up", KeyCode.LShift) }, output.Events);
        }

        [Fact]
        public async Task LegacyAndGestureOwnersDoNotReleaseEachOther()
        {
            var input = new InputStateService(new HoldKeyboard());
            var button = Create(input);
            await input.HoldAsync(KeyCode.LControl);
            await button.BeginAsync();
            await button.CancelAsync();
            Assert.True(input.IsHeld(KeyCode.LControl));
            await button.BeginAsync();
            await input.ReleaseAsync(KeyCode.LControl);
            Assert.True(input.IsHeld(KeyCode.LControl));
            await button.CancelAsync();
            Assert.False(input.IsHeld(KeyCode.LControl));
        }

        [Fact]
        public async Task LegacyToggleIsNotifiedWhenGestureOwnershipKeepsTheKeyDown()
        {
            var input = new InputStateService(new HoldKeyboard());
            var button = Create(input);
            await button.BeginAsync();
            var notifications = new List<bool>();
            input.Changed += (key, held) => notifications.Add(held);
            await input.ToggleAsync(KeyCode.LControl);
            Assert.Equal(new[] { true }, notifications);
            await button.CancelAsync();
            Assert.False(input.IsHeld(KeyCode.LControl));
        }

        [Fact]
        public async Task ShutdownReleasesInFlightGestureAndRejectsQueuedNewPress()
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var output = new HoldKeyboard { HoldGate = gate };
            var input = new InputStateService(output);
            var button = Create(input);
            var down = button.BeginAsync();
            var up = button.EndAsync(true);
            var next = button.BeginAsync();
            input.BeginShutdown();
            var shutdown = input.ReleaseAllAsync();
            gate.SetResult(true);
            await Task.WhenAll(down, up, next, shutdown).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(input.IsHeld(KeyCode.LControl));
            Assert.Equal(new[] { ("down", KeyCode.LControl), ("up", KeyCode.LControl) }, output.Events);
        }

        [Fact]
        public async Task SlowInjectionDoesNotChangeTapTimingOrReorderRapidTaps()
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var output = new HoldKeyboard { HoldGate = gate };
            var input = new InputStateService(output);
            var now = TimeSpan.Zero;
            var button = new PressHoldController(input, new[] { KeyCode.LControl }, true, 250, () => now);
            var down = button.BeginAsync();
            now += TimeSpan.FromMilliseconds(50);
            var up = button.EndAsync(true);
            var downAgain = button.BeginAsync();
            now += TimeSpan.FromMilliseconds(50);
            var upAgain = button.EndAsync(true);
            now += TimeSpan.FromSeconds(10);
            gate.SetResult(true);
            await Task.WhenAll(down, up, downAgain, upAgain).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { ("down", KeyCode.LControl), ("up", KeyCode.LControl) }, output.Events);
        }

        [Fact]
        public async Task CancellationWaitsForHoldThenAllowsANewGesture()
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var output = new HoldKeyboard { HoldGate = gate };
            var input = new InputStateService(output);
            var button = Create(input);
            var down = button.BeginAsync();
            var cancel = button.CancelAsync();
            var next = button.BeginAsync();
            gate.SetResult(true);
            await Task.WhenAll(down, cancel, next).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { ("down", KeyCode.LControl), ("up", KeyCode.LControl), ("down", KeyCode.LControl) }, output.Events);
            await button.CancelAsync();
        }

        [Fact]
        public async Task FailedHoldIsCleanedUpAndCannotBecomeATapLock()
        {
            var output = new HoldKeyboard { FailHold = true };
            var input = new InputStateService(output);
            var button = Create(input);
            await Assert.ThrowsAsync<InvalidOperationException>(() => button.BeginAsync());
            await button.EndAsync(true);
            Assert.False(input.IsHeld(KeyCode.LControl));
            Assert.False(input.SimulationInProgress);
            Assert.Equal(("up", KeyCode.LControl), output.Events.Last());
        }

        [Fact]
        public async Task FailedReleaseRemainsTrackedForShutdownRecovery()
        {
            var output = new HoldKeyboard();
            var input = new InputStateService(output);
            var button = Create(input);
            await button.BeginAsync();
            output.FailRelease = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => button.CancelAsync());
            Assert.True(input.IsHeld(KeyCode.LControl));
            output.FailRelease = false;
            await input.ReleaseAllAsync();
            Assert.False(input.IsHeld(KeyCode.LControl));
        }

        [Fact]
        public async Task PhysicalReleaseClearsLockAndNextTapCanLockAgain()
        {
            var input = new InputStateService(new HoldKeyboard());
            var button = Create(input);
            await button.BeginAsync();
            await button.EndAsync(true);
            input.ObservePhysicalKeyUp(KeyCode.LControl);
            Assert.False(button.IsActive);
            await button.BeginAsync();
            await button.EndAsync(true);
            Assert.True(button.IsActive);
            await input.ReleaseAllAsync();
            Assert.False(button.IsActive);
            await button.BeginAsync();
            Assert.False(button.IsActive);
        }

        private static PressHoldController Create(InputStateService input) =>
            new PressHoldController(input, new[] { KeyCode.LControl }, true, 250, () => TimeSpan.Zero);
    }

    internal sealed class HoldKeyboard : IKeyboardOutput
    {
        public readonly List<(string Action, KeyCode Key)> Events = new List<(string, KeyCode)>();
        public TaskCompletionSource<bool> HoldGate;
        public bool FailHold;
        public bool FailRelease;

        public async Task HoldAsync(KeyCode[] keys)
        {
            foreach (var key in keys) Events.Add(("down", key));
            if (HoldGate != null) await HoldGate.Task;
            if (FailHold) throw new InvalidOperationException("Partial hold failure");
        }

        public Task ReleaseAsync(KeyCode[] keys)
        {
            foreach (var key in keys) Events.Add(("up", key));
            if (FailRelease) throw new InvalidOperationException("Release failure");
            return Task.CompletedTask;
        }
    }
}
