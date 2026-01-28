namespace CobolToMySqlStudio.Application.Interfaces;

public interface ITransformEngine
{
    string GenerateSql(string sourceTable, string targetTableOrView, string dsl);
}
