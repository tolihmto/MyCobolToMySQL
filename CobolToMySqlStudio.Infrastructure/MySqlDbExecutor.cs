using System.Data;
using CobolToMySqlStudio.Application.Interfaces;
using MySqlConnector;

namespace CobolToMySqlStudio.Infrastructure;

public class MySqlDbExecutor : IDbExecutor
{
    private string _connectionString;

    public MySqlDbExecutor(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void ApplyConnectionString(string connectionString)
    {
        _connectionString = connectionString ?? string.Empty;
    }

    public string GetConnectionString() => _connectionString;

    public async Task<int> ExecuteNonQueryAsync(string sql, CancellationToken ct = default)
        => await ExecuteNonQueryAsync(sql, Array.Empty<KeyValuePair<string, object?>>(), ct);

    public async Task<int> ExecuteNonQueryAsync(string sql, IEnumerable<KeyValuePair<string, object?>> parameters, CancellationToken ct = default)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql, conn);
        foreach (var p in parameters)
        {
            cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        }
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> BulkInsertAsync(string tableName, IEnumerable<IDictionary<string, object?>> rows, CancellationToken ct = default)
    {
        var rowList = rows.ToList();
        if (rowList.Count == 0) return 0;
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        int total = 0;
        try
        {
            // Assume all rows share same columns
            var columns = rowList[0].Keys.ToList();
            string colList = string.Join(",", columns.Select(c => $"`{c}`"));
            const int batchSize = 500;
            for (int i = 0; i < rowList.Count; i += batchSize)
            {
                var batch = rowList.Skip(i).Take(batchSize).ToList();
                var valuesParts = new List<string>();
                var cmd = new MySqlCommand { Connection = conn, Transaction = tx };
                int paramIndex = 0;
                foreach (var row in batch)
                {
                    var valuePlaceholders = new List<string>();
                    foreach (var col in columns)
                    {
                        string pname = "@p" + (paramIndex++);
                        valuePlaceholders.Add(pname);
                        cmd.Parameters.AddWithValue(pname, row[col] ?? DBNull.Value);
                    }
                    valuesParts.Add("(" + string.Join(",", valuePlaceholders) + ")");
                }
                cmd.CommandText = $"INSERT INTO `{tableName}` ({colList}) VALUES {string.Join(",", valuesParts)}";
                total += await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            return total;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(string sql, CancellationToken ct = default)
    {
        var result = new List<Dictionary<string, object?>>();
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var val = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
                row[reader.GetName(i)] = val;
            }
            result.Add(row);
        }
        return result;
    }
}
