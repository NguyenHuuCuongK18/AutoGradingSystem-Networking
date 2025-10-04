using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace AutoGrading.Common.Services
{
    /// <summary>
    /// Handles exporting and reading test case and result XLSX files with formatted columns.
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

            // Format headers
            var headerRange = ws.Range("A1:D1");
            headerRange.Style.Fill.SetBackgroundColor(XLColor.LightBlue);
            headerRange.Style.Font.SetBold(true);
            headerRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            headerRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            int r = 2;
            foreach (var s in steps)
            {
                ws.Cell(r, 1).Value = s.Step;
                ws.Cell(r, 2).Value = s.Input;
                ws.Cell(r, 3).Value = s.ServerOutput;
                ws.Cell(r, 4).Value = s.ClientOutput;

                // Apply text wrapping and dynamic column width
                for (int c = 1; c <= 4; c++)
                {
                    var cell = ws.Cell(r, c);
                    cell.Style.Alignment.SetWrapText(true);
                    var content = cell.GetString().Length;
                    double width = content > 20 ? 15 + (Math.Ceiling((content - 20) / 10.0) * 0.5) : 15;
                    ws.Column(c).Width = Math.Max(ws.Column(c).Width, width);
                    cell.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                    cell.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
                }
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

        public static void CreateResultSpreadsheet(string resultPath, Dictionary<string, List<(int Step, string Input, string ExpectedClient, string ExpectedServer, string ActualClient, string ActualServer, bool Success)>> allTestResults, Dictionary<string, int> barem, int totalAwardedPoints, int totalMaxPoints)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? ".");

            using var wb = new XLWorkbook();

            // Sheet 1: Overall Info
            var overallSheet = wb.AddWorksheet("Overall Info");
            overallSheet.Cell(1, 1).Value = "Number of test cases";
            overallSheet.Cell(1, 2).Value = barem.Count;
            overallSheet.Cell(2, 1).Value = "Grade given";
            overallSheet.Cell(2, 2).Value = $"{totalAwardedPoints}/{totalMaxPoints}";

            // Format overall info headers
            var overallHeaderRange = overallSheet.Range("A1:B2");
            overallHeaderRange.Style.Fill.SetBackgroundColor(XLColor.LightBlue);
            overallHeaderRange.Style.Font.SetBold(true);
            overallHeaderRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            overallHeaderRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Subsequent sheets for each test case
            foreach (var kvp in allTestResults)
            {
                var testCaseName = kvp.Key;
                var results = kvp.Value;

                var ws = wb.AddWorksheet(testCaseName);
                ws.Cell(1, 1).Value = "Step";
                ws.Cell(1, 2).Value = "Input";
                ws.Cell(1, 3).Value = "Expected from Server";
                ws.Cell(1, 4).Value = "Expected from Client";
                ws.Cell(1, 5).Value = "Output Server";
                ws.Cell(1, 6).Value = "Output Client";
                ws.Cell(1, 7).Value = "Result";

                // Format headers
                var headerRange = ws.Range("A1:G1");
                headerRange.Style.Fill.SetBackgroundColor(XLColor.LightBlue);
                headerRange.Style.Font.SetBold(true);
                headerRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                headerRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                int r = 2;
                foreach (var row in results)
                {
                    ws.Cell(r, 1).Value = row.Step;
                    ws.Cell(r, 2).Value = row.Input; // ← FIX: Use actual input from test case
                    ws.Cell(r, 3).Value = row.ExpectedServer;
                    ws.Cell(r, 4).Value = row.ExpectedClient;
                    ws.Cell(r, 5).Value = row.ActualServer;
                    ws.Cell(r, 6).Value = row.ActualClient;
                    var resCell = ws.Cell(r, 7);
                    resCell.Value = row.Success ? "Success" : "Failed";

                    if (row.Success)
                    {
                        resCell.Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                    }
                    else
                    {
                        resCell.Style.Fill.SetBackgroundColor(XLColor.LightPink);
                        ws.Cell(r, 5).Style.Fill.SetBackgroundColor(XLColor.LightPink);
                        ws.Cell(r, 6).Style.Fill.SetBackgroundColor(XLColor.LightPink);
                    }

                    // Apply text wrapping and dynamic column width
                    for (int c = 1; c <= 7; c++)
                    {
                        var cell = ws.Cell(r, c);
                        cell.Style.Alignment.SetWrapText(true);
                        var content = cell.GetString().Length;
                        double width = content > 20 ? 15 + (Math.Ceiling((content - 20) / 10.0) * 0.5) : 15;
                        ws.Column(c).Width = Math.Max(ws.Column(c).Width, width);
                        cell.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                        cell.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
                    }
                    r++;
                }
            }

            wb.SaveAs(resultPath);
        }
    }
}