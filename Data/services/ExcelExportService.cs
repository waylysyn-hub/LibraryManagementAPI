using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Data.Services
{
    public class ExcelExportService
    {
        /// <summary>
        /// Export any list to a styled Excel sheet (table, autofilter, freeze header).
        /// </summary>
        public static MemoryStream ExportToExcel<T>(
            IEnumerable<T> data,
            List<(string Header, Func<T, object?> ValueSelector)> headers,
            string sheetName,
            string tableName = "Table1")
        {
            if (headers == null || headers.Count == 0)
                throw new ArgumentException("Headers must be provided");

            data ??= Enumerable.Empty<T>();

            var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName);

            // Header row
            for (int i = 0; i < headers.Count; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i].Header;
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data
            int row = 2;
            foreach (var item in data)
            {
                for (int col = 0; col < headers.Count; col++)
                {
                    var raw = headers[col].ValueSelector(item);

                    // منع حقن صيغ في Excel (لو كان النص يبدأ بـ = أو + أو -)
                    var s = raw?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(s) && (s.StartsWith("=") || s.StartsWith("+") || s.StartsWith("-") || s.StartsWith("@")))
                        s = "'" + s;

                    ws.Cell(row, col + 1).Value = s;
                }
                row++;
            }

            // Create table with style
            var lastRow = Math.Max(1, row - 1);
            var lastCol = headers.Count;
            var rng = ws.Range(1, 1, lastRow, lastCol);

            var table = rng.CreateTable(tableName);
            table.Theme = XLTableTheme.TableStyleMedium9;  // شكل معروف وأنيق
            table.ShowAutoFilter = true;

            // Freeze header
            ws.SheetView.FreezeRows(1);

            // Adjust widths
            ws.Columns(1, lastCol).AdjustToContents();

            // Optional: معلومات في الهيدر/الفوتر
            ws.PageSetup.Header.Right.AddText(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            ws.PageSetup.Footer.Center.AddText($"Exported by API • Rows: {lastRow - 1}");

            var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;
            return stream;
        }
    }
}
