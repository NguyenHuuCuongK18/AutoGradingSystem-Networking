using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace AutoGrading.Common.Services
{
    /// <summary>
    /// Manages header.xlsx with HeaderInfo (key-value, e.g., number_of_testcases) and PointBarem (testcase:points).
    /// Updates number_of_testcases on new test case addition and handles point updates for existing cases.
    /// Expansion: Add metadata like description or version to HeaderInfo.
    /// </summary>
    public static class HeaderManager
    {
        public const string HeaderInfoSheet = "HeaderInfo";
        public const string PointBaremSheet = "PointBarem";

        public static (Dictionary<string, string> headerInfo, Dictionary<string, int> barem) ReadGlobalHeader(string headerPath)
        {
            if (!File.Exists(headerPath))
                throw new FileNotFoundException("Global header not found", headerPath);

            var headerInfo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var barem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            using var wb = new XLWorkbook(headerPath);
            var infoSheet = wb.Worksheets.FirstOrDefault(ws => ws.Name.Equals(HeaderInfoSheet, StringComparison.OrdinalIgnoreCase));
            if (infoSheet != null)
            {
                foreach (var row in infoSheet.RowsUsed().Skip(1))
                {
                    var key = row.Cell(1).GetString().Trim();
                    var val = row.Cell(2).GetString();
                    if (!string.IsNullOrEmpty(key)) headerInfo[key] = val;
                }
            }

            var baremSheet = wb.Worksheets.FirstOrDefault(ws => ws.Name.Equals(PointBaremSheet, StringComparison.OrdinalIgnoreCase));
            if (baremSheet != null)
            {
                foreach (var row in baremSheet.RowsUsed().Skip(1))
                {
                    var testCaseName = row.Cell(1).GetString().Trim();
                    var ptsText = row.Cell(2).GetString().Trim();
                    if (string.IsNullOrEmpty(testCaseName)) continue;
                    if (int.TryParse(ptsText, out int pts)) barem[testCaseName] = pts;
                }
            }

            return (headerInfo, barem);
        }

        public static void WriteOrUpdateGlobalHeader(string headerPath, Dictionary<string, string> headerInfo, Dictionary<string, int> barem)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(headerPath) ?? ".");

            XLWorkbook wb;
            if (File.Exists(headerPath))
                wb = new XLWorkbook(headerPath);
            else
                wb = new XLWorkbook();

            var infoSheet = wb.Worksheets.FirstOrDefault(ws => ws.Name.Equals(HeaderInfoSheet, StringComparison.OrdinalIgnoreCase)) ?? wb.AddWorksheet(HeaderInfoSheet);
            infoSheet.Clear();
            infoSheet.Cell(1, 1).Value = "Key";
            infoSheet.Cell(1, 2).Value = "Value";
            int r = 2;
            foreach (var kv in headerInfo)
            {
                infoSheet.Cell(r, 1).Value = kv.Key;
                infoSheet.Cell(r, 2).Value = kv.Value;
                r++;
            }

            var baremSheet = wb.Worksheets.FirstOrDefault(ws => ws.Name.Equals(PointBaremSheet, StringComparison.OrdinalIgnoreCase)) ?? wb.AddWorksheet(PointBaremSheet);
            baremSheet.Clear();
            baremSheet.Cell(1, 1).Value = "TestCase";
            baremSheet.Cell(1, 2).Value = "TotalPoints";
            int br = 2;
            foreach (var kv in barem.OrderBy(x => x.Key))
            {
                baremSheet.Cell(br, 1).Value = kv.Key;
                baremSheet.Cell(br, 2).Value = kv.Value;
                br++;
            }

            wb.SaveAs(headerPath);
        }

        public static void UpdateOrAddBaremEntry(string headerPath, string testCaseName, int totalPoints)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(headerPath) ?? ".");
            XLWorkbook wb;
            if (File.Exists(headerPath))
                wb = new XLWorkbook(headerPath);
            else
                wb = new XLWorkbook();

            var baremSheet = wb.Worksheets.FirstOrDefault(ws => ws.Name.Equals(PointBaremSheet, StringComparison.OrdinalIgnoreCase)) ?? wb.AddWorksheet(PointBaremSheet);
            baremSheet.Cell(1, 1).Value = "TestCase";
            baremSheet.Cell(1, 2).Value = "TotalPoints";
            var rows = baremSheet.RowsUsed().Skip(1);
            var found = rows.FirstOrDefault(r => r.Cell(1).GetString().Trim().Equals(testCaseName, StringComparison.OrdinalIgnoreCase));
            if (found != null)
                found.Cell(2).Value = totalPoints;  // Update existing
            else
            {
                int next = baremSheet.LastRowUsed()?.RowNumber() ?? 1;
                baremSheet.Cell(next + 1, 1).Value = testCaseName;
                baremSheet.Cell(next + 1, 2).Value = totalPoints;  // Add new
            }

            var infoSheet = wb.Worksheets.FirstOrDefault(ws => ws.Name.Equals(HeaderInfoSheet, StringComparison.OrdinalIgnoreCase)) ?? wb.AddWorksheet(HeaderInfoSheet);
            infoSheet.Cell(1, 1).Value = "Key";
            infoSheet.Cell(1, 2).Value = "Value";
            var count = baremSheet.RowsUsed().Count() - 1;  // Exclude header
            var foundRow = infoSheet.RowsUsed().Skip(1).FirstOrDefault(r => r.Cell(1).GetString().Trim().Equals("number_of_testcases", StringComparison.OrdinalIgnoreCase));
            if (foundRow != null)
                foundRow.Cell(2).Value = count.ToString();
            else
            {
                int next = infoSheet.LastRowUsed()?.RowNumber() ?? 1;
                infoSheet.Cell(next + 1, 1).Value = "number_of_testcases";
                infoSheet.Cell(next + 1, 2).Value = count.ToString();
            }

            wb.SaveAs(headerPath);
        }
    }
}