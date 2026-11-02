using System;
using System.IO;
using TabletFriend.Data;
using TabletFriend.Models;

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
                try { ModelConverter.ConvertVector2(button.Value?.Size); }
                catch (FormatException e)
                {
                    throw new FormatException($"{Path.GetFileName(path)}, button '{button.Key}': {e.Message}", e);
                }
            }
        }
    }
}
