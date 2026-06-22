using TdsCertificatePortal.Models;
using System.Text;

namespace TdsCertificatePortal.Services;

public sealed class CsvParseResult
{
    public List<EmployeeInput> Employees { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class CsvEmployeeParser
{
    private static readonly string[] RequiredColumns =
    [
        "Employee Code",
        "Employee Name",
        "PAN Number",
        "Email Address"
    ];

    public async Task<CsvParseResult> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        var result = new CsvParseResult();
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        if (lines.Length == 0)
        {
            result.Errors.Add("Employee CSV is empty.");
            return result;
        }

        var headers = ParseLine(lines[0]).Select(h => h.Trim()).ToList();
        var headerIndex = headers
            .Select((name, index) => new { name, index })
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

        foreach (var column in RequiredColumns)
        {
            if (!headerIndex.ContainsKey(column))
            {
                result.Errors.Add($"Employee CSV is missing required column: {column}.");
            }
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseLine(line);
            string Read(string column)
            {
                var columnIndex = headerIndex[column];
                return columnIndex < values.Count ? values[columnIndex].Trim() : string.Empty;
            }

            var employee = new EmployeeInput
            {
                RowNumber = index + 1,
                EmployeeCode = Read("Employee Code"),
                EmployeeName = Read("Employee Name"),
                PanNumber = TextNormalization.NormalizePan(Read("PAN Number")),
                EmailAddress = Read("Email Address")
            };
            employee.NormalizedEmployeeName = TextNormalization.NormalizeName(employee.EmployeeName);

            if (string.IsNullOrWhiteSpace(employee.EmployeeCode))
            {
                result.Errors.Add($"Row {employee.RowNumber}: Employee Code is required.");
            }

            if (string.IsNullOrWhiteSpace(employee.EmployeeName))
            {
                result.Errors.Add($"Row {employee.RowNumber}: Employee Name is required.");
            }

            if (string.IsNullOrWhiteSpace(employee.PanNumber))
            {
                result.Errors.Add($"Row {employee.RowNumber}: PAN Number is required.");
            }

            result.Employees.Add(employee);
        }

        return result;
    }

    private static List<string> ParseLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values;
    }
}
