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

        /// <summary>
        /// Compares actual outputs against expected outputs for all steps and returns awarded points.
        /// Assumes points are divided evenly per step.
        /// Expansion: Add weighted scoring or custom diff thresholds.
        /// </summary>
        public static int CompareSteps(List<(int Step, string Input, string ServerOutput, string ClientOutput)> steps, (string ClientActual, string ServerActual) actualOutputs)
        {
            int awarded = 0;
            double pointsPerStep = 100.0 / steps.Count;  // Example total points; adjust as needed

            for (int i = 0; i < steps.Count; i++)
            {
                var expectedServer = steps[i].ServerOutput;
                var expectedClient = steps[i].ClientOutput;
                var actualServer = i < actualOutputs.ServerActual.Split('\n').Length ? actualOutputs.ServerActual.Split('\n')[i] : "";
                var actualClient = i < actualOutputs.ClientActual.Split('\n').Length ? actualOutputs.ClientActual.Split('\n')[i] : "";

                bool serverMatch = Normalize(expectedServer) == Normalize(actualServer);
                bool clientMatch = Normalize(expectedClient) == Normalize(actualClient);

                if (serverMatch && clientMatch)
                {
                    awarded += (int)Math.Ceiling(pointsPerStep);
                }
            }
            return awarded;
        }

        /// <summary>
        /// Checks if two outputs are equal after normalization.
        /// Expansion: Add tolerance for floating-point or custom matching.
        /// </summary>
        public static bool AreOutputsEqual(string expected, string actual)
        {
            return Normalize(expected) == Normalize(actual);
        }
    }
}