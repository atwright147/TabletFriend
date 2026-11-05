using System;
using System.IO;
using TabletFriend.Data;
using TabletFriend.Models;
using TabletFriend.Actions;

namespace TabletFriend
{
    internal static class LayoutValidator
    {
        internal static void Validate(LayoutData data, string path)
        {
            if (data?.Buttons == null)
                throw new FormatException($"{Path.GetFileName(path)}: missing buttons collection.");
            foreach (var button in data.Buttons)
            {
                try
                {
                    ModelConverter.ConvertVector2(button.Value?.Size);
                    var value = button.Value;
                    if (value == null || value.Spacer) continue;
                    if (value.QuickPressThresholdMs <= 0)
                        throw new FormatException("quick_press_threshold_ms must be positive.");
                    if (value.QuickPressToggle && !value.PressAndHold)
                        throw new FormatException("quick_press_toggle requires press_and_hold.");
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
    }
}
