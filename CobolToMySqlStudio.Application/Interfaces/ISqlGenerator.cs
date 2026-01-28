using CobolToMySqlStudio.Domain.Models;

namespace CobolToMySqlStudio.Application.Interfaces;

public interface ISqlGenerator
{
    string GenerateStagingTableDdl(string tableName, CopybookNode root);
}
