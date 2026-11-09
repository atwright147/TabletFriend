using System;
using System.Globalization;
using System.IO;
using TabletFriend.Data;
using TabletFriend.Models;
using TabletFriend.Actions;

namespace TabletFriend
{
    internal static class LayoutValidator
    {
        private static readonly string[] Styles =
            { "default", "accent", "circular_accent", "circular", "outlined", "shy", "none" };

        internal static void Validate(LayoutData data, string path)
        {
            if (data?.Buttons == null)
                throw new FormatException($"{Path.GetFileName(path)}: missing buttons collection.");
            if (data.LayoutWidth < 0)
                throw new FormatException($"{Path.GetFileName(path)}: layout_width must be at least 1.");
            if (data.ButtonSize is < LayoutModel.MinButtonSize)
                throw new FormatException($"{Path.GetFileName(path)}: button_size must be at least 8.");
            if (data.Margin is < LayoutModel.MinMargin)
                throw new FormatException($"{Path.GetFileName(path)}: margin must be nonnegative.");
            var min = ParseOpacity(data.MinOpacity, 0.2, "min_opacity", path);
            var max = ParseOpacity(data.MaxOpacity, 0.9, "max_opacity", path);
            if (min > max)
                throw new FormatException($"{Path.GetFileName(path)}: min_opacity must not exceed max_opacity.");
            foreach (var button in data.Buttons)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(button.Key))
                        throw new FormatException("Button name must not be empty.");
                    if (button.Value == null)
                        throw new FormatException("Button definition must be a mapping.");
                    ModelConverter.ConvertVector2(button.Value?.Size);
                    var value = button.Value;
                    if (value.Spacer)
                    {
                        if (value.Action != null || value.Actions != null || value.Text != null || value.Icon != null ||
                            value.Style != null || value.Font != null || value.FontSize != 0 || value.FontWeight != 0 ||
                            value.PressAndHold || value.QuickPressToggle || value.QuickPressThresholdMs != 250 ||
                            value.IconStretch != StretchData.None)
                            throw new FormatException("A spacer may only set spacer, size, and visibility.");
                        continue;
                    }
                    if (value.Action != null && value.Actions != null)
                        throw new FormatException("Use action or actions, not both.");
                    if (value.Action != null && string.IsNullOrWhiteSpace(value.Action))
                        throw new FormatException("action must not be empty.");
                    if (value.Actions != null && (value.Actions.Length == 0 || Array.Exists(value.Actions, string.IsNullOrWhiteSpace)))
                        throw new FormatException("actions must contain at least one nonempty action.");
                    if (value.Style != null && Array.IndexOf(Styles, value.Style) < 0)
                        throw new FormatException("style must be a supported button style.");
                    if (value.FontSize < 0)
                        throw new FormatException("font_size must be positive.");
                    if (value.FontWeight < 0 || value.FontWeight > 999)
                        throw new FormatException("font_weight must be between 1 and 999.");
                    if (value.QuickPressThresholdMs <= 0)
                        throw new FormatException("quick_press_threshold_ms must be positive.");
                    if (value.QuickPressToggle && !value.PressAndHold)
                        throw new FormatException("quick_press_toggle requires press_and_hold.");
                    if (value.QuickPressThresholdMs != 250 && !value.PressAndHold)
                        throw new FormatException("quick_press_threshold_ms requires press_and_hold.");
                    if (value.PressAndHold)
                    {
                        if (value.Actions?.Length > 0)
                            throw new FormatException("press_and_hold requires a plain key action, not an actions batch.");
                        using var action = ButtonActionResolver.Resolve(value.Action);
                        if (action?.GetType() != typeof(KeyAction))
                            throw new FormatException("press_and_hold requires a plain key action or chord, such as ctrl or ctrl+shift.");
                    }
                }
                catch (FormatException e)
                {
                    throw new FormatException($"{Path.GetFileName(path)}, button '{button.Key}': {e.Message}", e);
                }
            }
        }

        private static double ParseOpacity(string text, double fallback, string field, string path)
        {
            if (text == null) return fallback;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                !double.IsFinite(value) || value < 0 || value > 1)
                throw new FormatException($"{Path.GetFileName(path)}: {field} must be between 0 and 1.");
            return value;
        }
    }
}
