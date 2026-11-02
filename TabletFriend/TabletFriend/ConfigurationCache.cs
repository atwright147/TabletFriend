using System;
using System.Collections.Generic;
using System.IO;

namespace TabletFriend
{
    // Apply on the dispatcher when TModel contains WPF objects.
    internal sealed class ConfigurationCache<TData, TModel>
    {
        private Dictionary<string, TModel> _models = new Dictionary<string, TModel>(StringComparer.OrdinalIgnoreCase);

        public void Apply(DirectoryImport<TData> read, Func<TData, TModel> create, IDictionary<string, string> errors)
        {
            if (read.Error != null)
            {
                errors[read.Path] = read.Error;
                return;
            }
            var next = new Dictionary<string, TModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in read.Files)
            {
                var error = file.Error;
                if (error == null)
                {
                    try { next[file.Path] = create(file.Data); }
                    catch (Exception e) { error = e.Message; }
                }
                if (error != null)
                {
                    errors[file.Path] = error;
                    if (_models.TryGetValue(file.Path, out var previous)) next[file.Path] = previous;
                }
            }
            _models = next;
        }

        public Dictionary<string, TModel> ByName()
        {
            var result = new Dictionary<string, TModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var model in _models) result[Path.GetFileNameWithoutExtension(model.Key)] = model.Value;
            return result;
        }
    }
}
