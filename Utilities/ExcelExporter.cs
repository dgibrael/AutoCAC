using ClosedXML.Excel;

namespace AutoCAC.Utilities
{
    public class ExcelExporter
    {
        public static byte[] ExportDynamicToExcel(List<Dictionary<string, object>> rows, string sheetName = "Data")
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);

            var headers = rows.First().Keys.ToList();

            // Write headers
            for (int i = 0; i < headers.Count; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            // Write data rows
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                for (int colIndex = 0; colIndex < headers.Count; colIndex++)
                {
                    row.TryGetValue(headers[colIndex], out var value);
                    worksheet.Cell(rowIndex + 2, colIndex + 1).Value = value?.ToString() ?? "";
                }
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

    }
}
