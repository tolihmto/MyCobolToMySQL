using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CobolToMySqlStudio.Application.Services;
using CobolToMySqlStudio.Infrastructure;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace CobolToMySqlStudio.Tests;

public class IntegrationOptimalTests
{
    private static string? ResolveConnectionString()
    {
        // Prefer environment variable used by the UI
        var cs = Environment.GetEnvironmentVariable("COBOLSTUDIO_MYSQL");
        if (!string.IsNullOrWhiteSpace(cs)) return cs;

        // Try to read appsettings.json from UI project folder
        try
        {
            var uiAppsettings = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CobolToMySqlStudio.UI", "appsettings.json"));
            if (File.Exists(uiAppsettings))
            {
                var json = File.ReadAllText(uiAppsettings);
                // naive extraction
                var key = "\"MySql\"";
                var idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var colon = json.IndexOf(':', idx);
                    var quote1 = json.IndexOf('"', colon + 1);
                    var quote2 = json.IndexOf('"', quote1 + 1);
                    if (colon > 0 && quote1 > 0 && quote2 > quote1)
                    {
                        var val = json.Substring(quote1 + 1, quote2 - quote1 - 1);
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static string ResolveSamplesPath(string fileName)
    {
        var p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples", fileName));
        return p;
    }

    [Fact]
    public async Task Import_SampleOptimal_Into_Staging()
    {
        var cs = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            // No connection string available in environment or appsettings: skip test without failing.
            return;
        }

        // Ensure database and user exist with privileges for the provided connection string
        await EnsureDatabaseAndUserAsync(cs);

        var parser = new CopybookParser();
        var layout = new LayoutCalculator();
        var sqlGen = new SqlGenerator();
        var exec = new MySqlDbExecutor(cs);
        var import = new ImportService(exec, parser, layout);

        var copyPath = ResolveSamplesPath("sample_optimal.cpy");
        var dataPath = ResolveSamplesPath("sample_optimal.dat");
        File.Exists(copyPath).Should().BeTrue(copyPath + " not found");
        File.Exists(dataPath).Should().BeTrue(dataPath + " not found");

        // Validate data file: exactly 1000 non-empty lines, each exactly 74 chars (8+30+20+3+5+8)
        var allLines = await File.ReadAllLinesAsync(dataPath);
        var lines = allLines.Where(l => !string.IsNullOrEmpty(l)).ToArray();
        lines.Length.Should().Be(1000, "sample_optimal.dat must contain exactly 1000 non-empty records");
        lines.Should().OnlyContain(l => l.Length == 74, "each record must be fixed-width 74 characters");

        var text = await File.ReadAllTextAsync(copyPath);
        var ast = parser.Parse(text).Root;
        layout.ComputeOffsets(ast);

        var table = "staging_optimal";
        await exec.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS `{table}`");
        var ddl = sqlGen.GenerateStagingTableDdl(table, ast);
        await exec.ExecuteNonQueryAsync(ddl);

        var countBefore = await exec.QueryAsync($"SELECT COUNT(*) AS C FROM `{table}`");
        countBefore.Should().NotBeNull();

        var rowsInserted = await import.ImportWithAstAsync(dataPath, table, ast);
        var res = await exec.QueryAsync($"SELECT COUNT(*) AS C FROM `{table}`");
        res.Should().NotBeNull();
        var c = Convert.ToInt32(res.First()["C"]);
        c.Should().Be(1000);
    }

    private static async Task EnsureDatabaseAndUserAsync(string mainCs)
    {
        // Try quick connectivity first; if default database doesn't exist, try without it
        var server = GetCsValue(mainCs, "Server") ?? "127.0.0.1";
        var port = GetCsValue(mainCs, "Port") ?? "3306";
        var db = GetCsValue(mainCs, "Database") ?? "testdb";
        var user = GetCsValue(mainCs, "Uid") ?? GetCsValue(mainCs, "User Id") ?? "user";
        var pwd = GetCsValue(mainCs, "Pwd") ?? GetCsValue(mainCs, "Password") ?? string.Empty;

        bool Connected()
        {
            try { new MySqlDbExecutor(mainCs).ExecuteNonQueryAsync("SELECT 1").GetAwaiter().GetResult(); return true; } catch { return false; }
        }

        if (Connected()) return;

        // If we failed, try connecting without specifying Database (it may not exist yet)
        string csNoDb = $"Server={server};Port={port};Uid={user};Pwd={pwd};SslMode=None;";
        try
        {
            var probeNoDb = new MySqlDbExecutor(csNoDb);
            await probeNoDb.ExecuteNonQueryAsync("SELECT 1");
            // If user can connect but DB missing, create it if user has rights
            try
            {
                await probeNoDb.ExecuteNonQueryAsync($"CREATE DATABASE IF NOT EXISTS `{db}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;");
            }
            catch { /* ignore if no rights; will fallback to admin if available */ }
            // If we created the DB or it already exists, we are done
            if (Connected()) return;
        }
        catch { /* continue to admin path */ }

        // Admin connection string only if explicitly provided or root password env is set
        string? adminCs = Environment.GetEnvironmentVariable("COBOLSTUDIO_MYSQL_ADMIN");
        if (string.IsNullOrWhiteSpace(adminCs))
        {
            var rootPwd = Environment.GetEnvironmentVariable("COBOLSTUDIO_MYSQL_ROOT_PASSWORD");
            if (!string.IsNullOrWhiteSpace(rootPwd))
            {
                adminCs = $"Server={server};Port={port};Uid=root;Pwd={rootPwd};SslMode=None;";
            }
        }

        // If no admin credentials are available but main user is root with a password, use it as admin without DB
        if (string.IsNullOrWhiteSpace(adminCs) && string.Equals(user, "root", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(pwd))
        {
            adminCs = csNoDb; // root creds from main CS, without database
        }

        if (!string.IsNullOrWhiteSpace(adminCs))
        {
            var admin = new MySqlDbExecutor(adminCs);
            await admin.ExecuteNonQueryAsync($"CREATE DATABASE IF NOT EXISTS `{db}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;");
            await admin.ExecuteNonQueryAsync($"CREATE USER IF NOT EXISTS '{user}'@'%' IDENTIFIED BY '{pwd}';");
            await admin.ExecuteNonQueryAsync($"CREATE USER IF NOT EXISTS '{user}'@'localhost' IDENTIFIED BY '{pwd}';");
            await admin.ExecuteNonQueryAsync($"GRANT ALL PRIVILEGES ON `{db}`.* TO '{user}'@'%';");
            await admin.ExecuteNonQueryAsync($"GRANT ALL PRIVILEGES ON `{db}`.* TO '{user}'@'localhost';");
            await admin.ExecuteNonQueryAsync("FLUSH PRIVILEGES;");
            // Retry connectivity
            var probe2 = new MySqlDbExecutor(mainCs);
            await probe2.ExecuteNonQueryAsync("SELECT 1");
        }
    }

    private static string? GetCsValue(string cs, string key)
    {
        try
        {
            foreach (var part in cs.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                if (string.Equals(kv[0].Trim(), key, StringComparison.OrdinalIgnoreCase)) return kv[1].Trim();
            }
        }
        catch { }
        return null;
    }
}
