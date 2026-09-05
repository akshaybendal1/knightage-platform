using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Knightage.DeployUtility;

internal static partial class DbScriptRunner
{
    public static async Task RunScriptsAsync(DatabaseSettings settings, IReadOnlyList<string> scriptPaths)
    {
        await using var connection = new SqlConnection(settings.ToConnectionString());
        await connection.OpenAsync();

        foreach (var scriptPath in scriptPaths)
        {
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"DB script not found: {scriptPath}");
            }

            Console.WriteLine($"  Running {scriptPath} against {settings.Database} on {settings.Server}");
            var sql = await File.ReadAllTextAsync(scriptPath);

            foreach (var batch in SplitOnGo(sql))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;

                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 300;
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>SQL Server scripts commonly separate batches with a line containing only "GO". That's a convention of sqlcmd/SSMS, not real T-SQL, so ADO.NET has to split on it itself.</summary>
    internal static IEnumerable<string> SplitOnGo(string sql) => GoSeparator().Split(sql);

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}
