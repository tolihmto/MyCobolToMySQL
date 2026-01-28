using System.Collections.Generic;

namespace CobolToMySqlStudio.Domain.Models;

public class CopybookNode
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? Picture { get; set; }
    public bool IsNumeric { get; set; }
    public bool IsSigned { get; set; }
    public int Scale { get; set; }
    public int StorageLength { get; set; }
    public int DisplayLength { get; set; }
    public UsageType Usage { get; set; } = UsageType.Display;
    public int? Occurs { get; set; }
    public string? Redefines { get; set; }
    public int Offset { get; set; }
    public bool IsGroup { get; set; }
    public bool IsFiller => Name.Equals("FILLER", System.StringComparison.OrdinalIgnoreCase);
    public List<CopybookNode> Children { get; set; } = new();
    public CopybookNode? Parent { get; set; }
}
