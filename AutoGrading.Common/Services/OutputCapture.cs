using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AutoGrading.Common.Services
{
    /// <summary>
    /// Captures output lines in a thread-safe buffer.
    /// Expansion: Add timestamping, filtering, or max buffer size.
    /// </summary>
    public class OutputCapture
    {
        private readonly List<string> _buffer = new();
        private readonly object _sync = new();

        public void Append(string text)
        {
            lock (_sync) { _buffer.Add(text); }
        }

        public string ToSingleString()
        {
            lock (_sync) return string.Join("", _buffer);
        }

        public async Task SaveToFileAsync(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            string[] snapshot;
            lock (_sync) snapshot = _buffer.ToArray();
            await File.WriteAllLinesAsync(path, snapshot).ConfigureAwait(false);
        }

        public IReadOnlyList<string> GetAll()
        {
            lock (_sync) return _buffer.AsReadOnly();
        }

        public void Clear()
        {
            lock (_sync) _buffer.Clear();
        }
    }
}