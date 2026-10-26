using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Knightage.Platform.Core.Interfaces;
using Microsoft.Data.SqlClient;

namespace Knightage.Platform.Service;

public class TenantDataService : ITenantDataService
{
    public async Task<string> ExportAsync(string connectionString, IReadOnlyList<string> tables, Guid filterValue, string filterColumn = "OrganizationId", CancellationToken cancellationToken = default)
    {
        var script = new StringBuilder();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var table in tables)
        {
            script.AppendLine($"-- {table}");
            await using var command = new SqlCommand($"SELECT * FROM [{table}] WHERE [{filterColumn}] = @FilterValue", connection);
            command.Parameters.AddWithValue("@FilterValue", filterValue);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var columnNames = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            var columnList = string.Join(", ", columnNames.Select(c => $"[{c}]"));

            var rowCount = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                var values = new string[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    values[i] = FormatSqlLiteral(reader.GetValue(i));
                }

                script.AppendLine($"INSERT INTO [{table}] ({columnList}) VALUES ({string.Join(", ", values)});");
                rowCount++;
            }

            script.AppendLine($"-- {rowCount} row(s)");
            script.AppendLine("GO");
            script.AppendLine();
        }

        return script.ToString();
    }

    public async Task ExecuteScriptAsync(string connectionString, string script, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Migrations in this repo are already written as GO-separated batches -- split the
        // same way sqlcmd/SSMS would rather than send the whole thing as one statement,
        // since a batch can't mix DDL like CREATE PROCEDURE with other statements around it.
        // A line-based regex (not a fixed substring pattern) handles any mix of \r\n/\n line
        // endings and extra blank lines around GO, which a concatenation of ~48 files
        // (each with their own formatting) can't be assumed to avoid. The trailing \r? is
        // load-bearing: multiline $ anchors right before \n, not before a \r that precedes
        // it, so a CRLF-terminated "GO" line (e.g. every line StringBuilder.AppendLine emits
        // on Windows -- exactly what ExportAsync's generated data scripts are made of) would
        // otherwise never match and the whole script would silently execute as one batch.
        var batches = Regex.Split(script, @"^[ \t]*GO[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Select(b => b.Trim())
            .Where(b => b.Length > 0);

        foreach (var batch in batches)
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(string connectionString, IReadOnlyList<string> tables, Guid filterValue, string filterColumn = "OrganizationId", CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var table in tables)
        {
            await using var command = new SqlCommand($"DELETE FROM [{table}] WHERE [{filterColumn}] = @FilterValue", connection);
            command.Parameters.AddWithValue("@FilterValue", filterValue);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyDictionary<string, int>> CountRowsAsync(string connectionString, IReadOnlyList<string> tables, Guid filterValue, string filterColumn = "OrganizationId", CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<string, int>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var table in tables)
        {
            await using var command = new SqlCommand($"SELECT COUNT(*) FROM [{table}] WHERE [{filterColumn}] = @FilterValue", connection);
            command.Parameters.AddWithValue("@FilterValue", filterValue);
            counts[table] = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        }

        return counts;
    }

    private static string FormatSqlLiteral(object value)
    {
        if (value is DBNull or null)
        {
            return "NULL";
        }

        return value switch
        {
            string s => $"N'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            byte or short or int or long => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            float or double or decimal => Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            Guid g => $"'{g}'",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'",
            DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss.fff zzz}'",
            byte[] bytes => "0x" + Convert.ToHexString(bytes),
            _ => $"N'{value.ToString()!.Replace("'", "''")}'",
        };
    }
}
