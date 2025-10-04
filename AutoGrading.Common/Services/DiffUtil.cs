using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace AutoGrading.Common.Services
{
    /// <summary>
    /// Utilities for normalizing and diffing outputs.
    /// Normalization: Handles line endings, whitespace, empty lines.
    /// Expansion: Add custom prompt removal or case-insensitive comparison.
    /// </summary>
    public static class DiffUtil
    {
        private static readonly Regex MultiWhitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = raw.Split('\n').Select(l => MultiWhitespace.Replace(l, " ").Trim()).Where(l => !string.IsNullOrWhiteSpace(l));
            return string.Join("\n", lines);
        }

        public static string? GetUnifiedDiffString(string expected, string actual)
        {
            var nExp = Normalize(expected);
            var nAct = Normalize(actual);
            if (nExp == nAct) return null;

            var d = new Differ();
            var builder = new InlineDiffBuilder(d);
            var diff = builder.BuildDiffModel(nExp, nAct);
            var sb = new StringBuilder();
            foreach (var line in diff.Lines)
            {
                switch (line.Type)
                {
                    case ChangeType.Inserted: sb.AppendLine($"+ {line.Text}"); break;
                    case ChangeType.Deleted: sb.AppendLine($"- {line.Text}"); break;
                    case ChangeType.Unchanged: sb.AppendLine($"  {line.Text}"); break;
                    case ChangeType.Modified: sb.AppendLine($"~ {line.Text}"); break;
                }
            }
            return sb.ToString();
        }
    }
}