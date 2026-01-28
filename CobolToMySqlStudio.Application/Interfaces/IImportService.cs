namespace CobolToMySqlStudio.Application.Interfaces;

public interface IImportService
{
    Task ImportAsync(string filePath, string tableName, CancellationToken ct = default);
    Task<int> ImportWithAstAsync(string dataFilePath, string tableName, CobolToMySqlStudio.Domain.Models.CopybookNode root, CancellationToken ct = default);
}
