using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TabletFriend
{
    // Check the YAML shape before deserialization coerces scalar values or loses
    // the distinction between an omitted field and an explicit null.
    internal static class LayoutSyntaxValidator
    {
        private static readonly HashSet<string> RootFields = new HashSet<string>(StringComparer.Ordinal)
            { "layout_width", "button_size", "margin", "min_opacity", "max_opacity", "app", "buttons" };
        private static readonly HashSet<string> ButtonFields = new HashSet<string>(StringComparer.Ordinal)
            { "action", "actions", "press_and_hold", "quick_press_toggle", "quick_press_threshold_ms",
              "text", "icon", "icon_stretch", "size", "style", "visibility", "spacer",
              "font", "font_size", "font_weight" };
        private static readonly HashSet<string> SpacerFields = new HashSet<string>(StringComparer.Ordinal)
            { "spacer", "size", "visibility" };
        private static readonly HashSet<string> VisibilityValues = new HashSet<string>(StringComparer.Ordinal)
            { "always", "docked", "undocked", "docked_left", "docked_right", "docked_top" };
        private static readonly HashSet<string> IconStretchValues = new HashSet<string>(StringComparer.Ordinal)
            { "none", "fill", "fit" };

        internal static void Validate(string yaml, string path)
        {
            var stream = new YamlStream();
            try { stream.Load(new StringReader(yaml)); }
            catch (YamlException e) { throw new FormatException($"{Path.GetFileName(path)}: {e.Message}", e); }
            if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
                throw new FormatException($"{Path.GetFileName(path)}: layout must be a mapping.");
            foreach (var item in root.Children)
            {
                var name = Name(item.Key, path, null);
                if (!RootFields.Contains(name))
                    throw Error(path, null, $"unknown layout field '{name}'.");
                if (name == "buttons")
                {
                    if (item.Value is not YamlMappingNode buttons)
                        throw Error(path, null, "buttons must be a mapping.");
                    foreach (var button in buttons.Children)
                        ValidateButton(Name(button.Key, path, null), button.Value, path);
                }
                else ValidateScalar(name, item.Value, path, null);
            }
        }

        private static void ValidateButton(string name, YamlNode node, string path)
        {
            if (node is not YamlMappingNode fields)
                throw Error(path, name, "button definition must be a mapping.");
            var spacer = false;
            foreach (var field in fields.Children)
                if (Name(field.Key, path, name) == "spacer" && field.Value is YamlScalarNode marker &&
                    string.Equals(marker.Value, "true", StringComparison.OrdinalIgnoreCase))
                    spacer = true;
            if (spacer)
                foreach (var field in fields.Children)
                    if (!SpacerFields.Contains(Name(field.Key, path, name)))
                        throw Error(path, name, "A spacer may only set spacer, size, and visibility.");
            var hasThreshold = false;
            var pressAndHold = false;
            foreach (var field in fields.Children)
            {
                var key = Name(field.Key, path, name);
                if (!ButtonFields.Contains(key))
                    throw Error(path, name, $"unknown button field '{key}'.");
                if (key == "actions")
                {
                    if (field.Value is not YamlSequenceNode actions)
                        throw Error(path, name, "actions must be a sequence.");
                    foreach (var action in actions.Children)
                        ValidateScalar("action", action, path, name);
                }
                else ValidateScalar(key, field.Value, path, name);
                if (key == "quick_press_threshold_ms") hasThreshold = true;
                if (key == "press_and_hold")
                    pressAndHold = string.Equals(((YamlScalarNode)field.Value).Value, "true", StringComparison.OrdinalIgnoreCase);
            }
            if (hasThreshold && !pressAndHold)
                throw Error(path, name, "quick_press_threshold_ms requires press_and_hold.");
        }

        private static void ValidateScalar(string key, YamlNode node, string path, string button)
        {
            if (node is not YamlScalarNode scalar ||
                scalar.Style == ScalarStyle.Plain &&
                (string.IsNullOrEmpty(scalar.Value) || scalar.Value is "null" or "~"))
                throw Error(path, button, $"{key} must have a value of the correct type.");
            var plain = scalar.Style == ScalarStyle.Plain;
            var type = key switch
            {
                "layout_width" or "button_size" or "margin" or "quick_press_threshold_ms" or "font_size" or "font_weight" => "integer",
                "min_opacity" or "max_opacity" => "number",
                "press_and_hold" or "quick_press_toggle" or "spacer" => "boolean",
                _ => "string"
            };
            var valid = type switch
            {
                "integer" => plain && int.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
                "number" => plain && double.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
                "boolean" => plain && bool.TryParse(scalar.Value, out _),
                _ => !plain || !bool.TryParse(scalar.Value, out _) &&
                    !double.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            };
            if (!valid) throw Error(path, button, $"{key} must be a {type}.");
            if (type == "integer")
            {
                var number = int.Parse(scalar.Value, CultureInfo.InvariantCulture);
                if (key == "quick_press_threshold_ms" && number < 1)
                    throw Error(path, button, "quick_press_threshold_ms must be positive.");
                if ((key is "layout_width" or "font_size" or "font_weight") && number < 1 ||
                    key == "button_size" && number < 8 || key == "margin" && number < 0)
                    throw Error(path, button, $"{key} is outside its allowed range.");
            }
            if (key == "visibility" && !VisibilityValues.Contains(scalar.Value) ||
                key == "icon_stretch" && !IconStretchValues.Contains(scalar.Value))
                throw Error(path, button, $"{key} has an unsupported value.");
        }

        private static string Name(YamlNode node, string path, string button)
        {
            if (node is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
                throw Error(path, button, "field or button name must be nonempty text.");
            return scalar.Value;
        }

        private static FormatException Error(string path, string button, string message) =>
            new FormatException($"{Path.GetFileName(path)}{(button == null ? "" : $", button '{button}'")}: {message}");
    }
}
