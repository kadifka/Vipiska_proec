using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using VupisjkaWpf.Models;

namespace VupisjkaWpf.Services
{
    public static class ExcelService
    {
        public static List<PersonRow> ReadPeople(string path)
        {
            var rows = new List<PersonRow>();

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet(1);
            var used = ws.RangeUsed();
            if (used == null) return rows;

            var firstRow = used.FirstRowUsed();
            int colFio = -1, colNum = -1;

            foreach (var cell in firstRow.Cells())
            {
                var name = (cell.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                if (name.Contains("фио") || name == "fio") colFio = cell.Address.ColumnNumber;
                if (name.Contains("личный номер") || name.Contains("личный_номер") || name.Contains("личный-номер"))
                    colNum = cell.Address.ColumnNumber;
            }

            foreach (var row in used.RowsUsed().Skip(1))
            {
                string fio = colFio > 0 ? row.Cell(colFio).GetString().Trim() : string.Empty;
                string num = colNum > 0 ? row.Cell(colNum).GetString().Trim() : string.Empty;

                if (!string.IsNullOrWhiteSpace(fio) || !string.IsNullOrWhiteSpace(num))
                    rows.Add(new PersonRow(fio, num));
            }

            return rows;
        }
    }
}
