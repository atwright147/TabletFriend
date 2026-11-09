using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Json.Schema;
using TabletFriend.Data;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TabletFriend.Tests
{
    public class LayoutSchemaTests
    {
        private static readonly JsonSchema Schema = JsonSchema.Build(
            JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schemas", "layout.schema.json"))).RootElement);

        [Fact]
        public void BundledLayoutsPassSchemaAndRuntimeValidation()
        {
            var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "files", "layouts"), "*.yaml");
            Assert.NotEmpty(files);
            foreach (var file in files)
            {
                var yaml = File.ReadAllText(file);
                Assert.True(Conforms(yaml), Path.GetFileName(file) + " does not match the layout schema.");
                Assert.Null(Importer.Parse<LayoutData>(file, yaml).Error);
            }
        }

        [Theory]
        [InlineData("buttons: {}")]
        [InlineData("buttons:\n  label:\n    text: Hello")]
        [InlineData("buttons:\n  held:\n    action: ctrl\n    press_and_hold: true\n    quick_press_toggle: true\n    quick_press_threshold_ms: 250")]
        public void MinimalAndHeldLayoutsPassBothValidators(string yaml)
        {
            Assert.True(Conforms(yaml));
            Assert.Null(Importer.Parse<LayoutData>("layout.yaml", yaml).Error);
        }

        [Theory]
        [InlineData("layout_wdth: 4\nbuttons: {}")]
        [InlineData("layout_width: 0\nbuttons: {}")]
        [InlineData("layout_width: null\nbuttons: {}")]
        [InlineData("button_size: '12'\nbuttons: {}")]
        [InlineData("button_size: 7\nbuttons: {}")]
        [InlineData("margin: -1\nbuttons: {}")]
        [InlineData("min_opacity: 1.1\nbuttons: {}")]
        [InlineData("buttons:\n  bad:\n    styl: accent")]
        [InlineData("buttons:\n  bad:\n    size: 0,1")]
        [InlineData("buttons:\n  bad:\n    style: unknown")]
        [InlineData("buttons:\n  bad:\n    visibility: Docked")]
        [InlineData("buttons:\n  bad:\n    icon_stretch: stretch")]
        [InlineData("buttons:\n  bad:\n    action: 1")]
        [InlineData("buttons:\n  bad:\n    action: null")]
        [InlineData("buttons:\n  bad:\n    actions: []")]
        [InlineData("buttons:\n  bad:\n    actions: [1]")]
        [InlineData("buttons:\n  bad:\n    action: ctrl\n    actions: [shift]")]
        [InlineData("buttons:\n  bad:\n    spacer: true\n    action: ctrl")]
        [InlineData("buttons:\n  bad:\n    spacer: true\n    press_and_hold: false")]
        [InlineData("buttons:\n  bad:\n    font_size: -1")]
        [InlineData("buttons:\n  bad:\n    font_weight: 1000")]
        [InlineData("buttons:\n  bad:\n    quick_press_toggle: true\n    action: ctrl")]
        [InlineData("buttons:\n  bad:\n    quick_press_threshold_ms: 250\n    action: ctrl")]
        public void InvalidLayoutsFailBothValidators(string yaml)
        {
            Assert.False(Conforms(yaml));
            Assert.NotNull(Importer.Parse<LayoutData>("invalid.yaml", yaml).Error);
        }

        [Fact]
        public void RuntimeAlsoChecksRulesTheSchemaCannotExpress()
        {
            foreach (var yaml in new[]
            {
                "min_opacity: 0.9\nmax_opacity: 0.2\nbuttons: {}",
                "buttons:\n  bad:\n    action: toggle ctrl\n    press_and_hold: true"
            })
                Assert.NotNull(Importer.Parse<LayoutData>("invalid.yaml", yaml).Error);
        }

        [Fact]
        public void DuplicateButtonNamesAreRejectedBeforeDeserialization()
        {
            var yaml = "buttons:\n  same:\n    text: First\n  same:\n    text: Second\n";
            Assert.NotNull(Importer.Parse<LayoutData>("duplicate.yaml", yaml).Error);
        }

        private static bool Conforms(string yaml)
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml.Replace("\t", "  ")));
            return Schema.Evaluate(JsonSerializer.SerializeToElement(Convert(stream.Documents[0].RootNode))).IsValid;
        }

        private static object Convert(YamlNode node)
        {
            if (node is YamlMappingNode mapping)
            {
                var entries = new Dictionary<string, object>();
                foreach (var entry in mapping.Children)
                    entries[((YamlScalarNode)entry.Key).Value] = Convert(entry.Value);
                return entries;
            }
            if (node is YamlSequenceNode sequence)
            {
                var entries = new List<object>();
                foreach (var entry in sequence.Children) entries.Add(Convert(entry));
                return entries;
            }
            var scalar = (YamlScalarNode)node;
            if (scalar.Style != ScalarStyle.Plain) return scalar.Value;
            if (string.IsNullOrEmpty(scalar.Value) || scalar.Value is "null" or "~") return null;
            if (bool.TryParse(scalar.Value, out var boolean)) return boolean;
            if (long.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return integer;
            if (double.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return number;
            return scalar.Value;
        }
    }
}
