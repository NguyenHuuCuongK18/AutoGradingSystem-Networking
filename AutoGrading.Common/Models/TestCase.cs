using System.Collections.Generic;

namespace AutoGrading.Common.Models
{
    /// <summary>
    /// Represents a test case with name, path, points, inputs, and expected outputs per step.
    /// Expansion: Add properties for timeouts, metadata, or custom comparison rules.
    /// </summary>
    public class TestCase
    {
        public string Name { get; set; } = "";
        public string FolderPath { get; set; } = "";
        public string TestCaseXlsxPath => System.IO.Path.Combine(FolderPath, "testcase.xlsx");
        public int TotalPoints { get; set; } = 0;
        public List<string> Inputs { get; set; } = new List<string>();
        public List<(string ExpectedClientOutput, string ExpectedServerOutput)> ExpectedOutputs { get; set; } =
            new List<(string, string)>();
    }
}