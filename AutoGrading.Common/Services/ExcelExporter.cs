using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace AutoGrading.Common.Services
{
    /// <summary>
    /// Handles exporting and reading test case and result XLSX files.
    /// Expansion: Add custom sheets, formatting, or diff columns.
    /// </summary>
    public static class ExcelExporter
    {
        public static void CreateTestCaseFile(string testcaseXlsxPath, string testCaseName, IList<(int Step, string Input, string ServerOutput, string ClientOutput)> steps)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(testcaseXlsxPath) ?? ".");

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Steps");
            ws.Cell(1, 1).Value = "Step";
            ws.Cell(1, 2).Value = "Input";
            ws.Cell(1, 3).Value = "Server Output";
            ws.Cell(1, 4).Value = "Client Output";

            int r = 2;
            foreach (var s in steps)
            {
                ws.Cell(r, 1).Value = s.Step;
                ws.Cell(r, 2).Value = s.Input;
                ws.Cell(r, 3).Value = s.ServerOutput;
                ws.Cell(r, 4).Value = s.ClientOutput;
                r++;
            }

            var meta = wb.AddWorksheet("Meta");
            meta.Cell(1, 1).Value = "TestCaseName";
            meta.Cell(1, 2).Value = testCaseName;
            meta.Cell(2, 1).Value = "Generated";
            meta.Cell(2, 2).Value = DateTime.UtcNow.ToString("o");

            wb.SaveAs(testcaseXlsxPath);
        }

        public static List<(int Step, string Input, string ServerOutput, string ClientOutput)> ReadTestCaseFile(string testcaseXlsxPath)
        {
            var list = new List<(int, string, string, string)>();
            if (!File.Exists(testcaseXlsxPath)) return list;

            using var wb = new XLWorkbook(testcaseXlsxPath);
            var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Equals("Steps", StringComparison.OrdinalIgnoreCase));
            if (ws == null) return list;

            var rows = ws.RowsUsed().Skip(1);
            foreach (var r in rows)
            {
                int step = 0;
                int.TryParse(r.Cell(1).GetString().Trim(), out step);
                var input = r.Cell(2).GetString();
                var server = r.Cell(3).GetString();
                var client = r.Cell(4).GetString();
                list.Add((step, input, server, client));
            }
            return list;
        }

        public static void CreateResultSpreadsheet(string resultPath, IEnumerable<(int Step, string ExpectedClient, string ExpectedServer, string ActualClient, string ActualServer, bool Success)> results, string testCaseName, int awardedPoints, int totalPoints)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? ".");

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Results");
            ws.Cell(1, 1).Value = "Test Step";
            ws.Cell(1, 2).Value = "Expected Output (Client)";
            ws.Cell(1, 3).Value = "Expected Output (Server)";
            ws.Cell(1, 4).Value = "Actual Output (Client)";
            ws.Cell(1, 5).Value = "Actual Output (Server)";
            ws.Cell(1, 6).Value = "Result";

            int r = 2;
            foreach (var row in results)
            {
                ws.Cell(r, 1).Value = row.Step;
                ws.Cell(r, 2).Value = row.ExpectedClient;
                ws.Cell(r, 3).Value = row.ExpectedServer;
                ws.Cell(r, 4).Value = row.ActualClient;
                ws.Cell(r, 5).Value = row.ActualServer;
                var resCell = ws.Cell(r, 6);
                resCell.Value = row.Success ? "Success" : "Failed";

                if (row.Success)
                {
                    resCell.Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                }
                else
                {
                    resCell.Style.Fill.SetBackgroundColor(XLColor.LightPink);
                    ws.Cell(r, 4).Style.Fill.SetBackgroundColor(XLColor.LightPink);
                    ws.Cell(r, 5).Style.Fill.SetBackgroundColor(XLColor.LightPink);
                }
                r++;
            }

            var meta = wb.AddWorksheet("Summary");
            meta.Cell(1, 1).Value = "TestCase";
            meta.Cell(1, 2).Value = testCaseName;
            meta.Cell(2, 1).Value = "AwardedPoints";
            meta.Cell(2, 2).Value = awardedPoints;
            meta.Cell(3, 1).Value = "TotalPoints";
            meta.Cell(3, 2).Value = totalPoints;
            meta.Cell(4, 1).Value = "Generated";
            meta.Cell(4, 2).Value = DateTime.UtcNow.ToString("o");

            wb.SaveAs(resultPath);
        }
    }
}