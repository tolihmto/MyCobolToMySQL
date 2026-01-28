using System.Data;
using System.Threading.Tasks;

namespace CobolToMySqlStudio.Application.Interfaces;

public interface IDbExecutor
{
    Task<int> ExecuteNonQueryAsync(string sql, CancellationToken ct = default);
    Task<int> ExecuteNonQueryAsync(string sql, IEnumerable<KeyValuePair<string, object?>> parameters, CancellationToken ct = default);
    Task<int> BulkInsertAsync(string tableName, IEnumerable<IDictionary<string, object?>> rows, CancellationToken ct = default);
    Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(string sql, CancellationToken ct = default);
}
