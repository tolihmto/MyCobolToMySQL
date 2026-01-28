using CobolToMySqlStudio.Domain.Models;

namespace CobolToMySqlStudio.Application.Interfaces;

public interface ICopybookParser
{
    CopybookParseResult Parse(string text);
}

public class CopybookParseResult
{
    public CopybookNode Root { get; set; } = new();
    public int RecordLength { get; set; }
}
