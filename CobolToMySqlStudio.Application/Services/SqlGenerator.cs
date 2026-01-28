using System.Text;
using CobolToMySqlStudio.Application.Interfaces;
using CobolToMySqlStudio.Domain.Models;

namespace CobolToMySqlStudio.Application.Services;

public class SqlGenerator : ISqlGenerator
{
    public string GenerateStagingTableDdl(string tableName, CopybookNode root)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS `{tableName}` (");
        sb.AppendLine("  `Id` BIGINT NOT NULL AUTO_INCREMENT,");
        foreach (var leaf in EnumerateLeaves(root))
        {
            if (leaf.IsFiller) continue;
            var (type, details) = MapType(leaf);
            sb.Append("  `").Append(SafeName(leaf)).Append("` ").Append(type);
            if (!string.IsNullOrEmpty(details)) sb.Append(details);
            sb.AppendLine(",");
        }
        sb.AppendLine("  `ImportFileName` VARCHAR(255) NULL,");
        sb.AppendLine("  PRIMARY KEY (`Id`)");
        sb.AppendLine(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");
        return sb.ToString();
    }

    private static IEnumerable<CopybookNode> EnumerateLeaves(CopybookNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.IsGroup)
            {
                foreach (var c in EnumerateLeaves(child)) yield return c;
            }
            else
            {
                yield return child;
            }
        }
    }

    private static string SafeName(CopybookNode node)
        => node.Name.Replace('-', '_');

    private static (string Type, string Details) MapType(CopybookNode field)
    {
        // Simplified mapping
        if (string.IsNullOrWhiteSpace(field.Picture)) return ("VARCHAR(255)", string.Empty);
        var pic = field.Picture.ToUpperInvariant();
        if (pic.StartsWith("X"))
        {
            var len = ExtractNumberAfter(pic, 'X');
            return (len <= 255 ? $"VARCHAR({len})" : $"TEXT", string.Empty);
        }
        if (pic.Contains('V'))
        {
            var parts = pic.Split('V');
            int left = ExtractNumberAfter(parts[0], '9');
            int right = ExtractNumberAfter(parts[1], '9');
            int p = left + right + (field.IsSigned ? 1 : 0);
            return ($"DECIMAL({p},{right})", string.Empty);
        }
        int digits = ExtractNumberAfter(pic, '9');
        if (digits > 0)
        {
            if (digits <= 9) return ("INT", string.Empty);
            if (digits <= 18) return ("BIGINT", string.Empty);
            return ($"DECIMAL({digits},0)", string.Empty);
        }
        return ("VARCHAR(255)", string.Empty);
    }

    private static int ExtractNumberAfter(string pic, char c)
    {
        int idx = pic.IndexOf(c);
        if (idx < 0) return 0;
        int open = pic.IndexOf('(', idx);
        int close = pic.IndexOf(')', open + 1);
        if (open > 0 && close > open)
        {
            if (int.TryParse(pic.Substring(open + 1, close - open - 1), out int val)) return val;
        }
        int count = 0;
        foreach (var ch in pic) if (ch == c) count++;
        return count;
    }
}
