using CobolToMySqlStudio.Application.Interfaces;
using System.Text;

namespace CobolToMySqlStudio.Application.Services;

public class TransformEngine : ITransformEngine
{
    // Minimal DSL translator: supports MOVE, COMPUTE with +,-,*,/, IF ... THEN ... ELSE ..., DATE8->DATE
    public string GenerateSql(string sourceTable, string targetTableOrView, string dsl)
    {
        // Build the projection list solely from DSL to avoid duplicate column names (no implicit s.*)
        var select = new StringBuilder();
        select.AppendLine($"CREATE OR REPLACE VIEW `{targetTableOrView}` AS");
        var projections = new List<string>();
        foreach (var line in dsl.Replace("\r\n", "\n").Split('\n'))
        {
            var t = line.Trim();
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (t.StartsWith("--")) continue;
            var sqlExpr = TranslateLine(t);
            if (!string.IsNullOrEmpty(sqlExpr)) projections.Add(sqlExpr);
        }
        if (projections.Count == 0)
        {
            // Fallback: select all source columns
            select.AppendLine($"SELECT s.*");
        }
        else
        {
            select.Append("SELECT ").Append(string.Join(", ", projections)).AppendLine();
        }
        select.AppendLine($"FROM `{sourceTable}` s;");
        return select.ToString();
    }

    private static string TranslateLine(string t)
    {
        // MOVE target = source
        if (t.StartsWith("MOVE", StringComparison.OrdinalIgnoreCase))
        {
            // MOVE Source TO Target
            // or MOVE field -> field
            var parts = t.Replace("MOVE", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("TO", ",", StringComparison.OrdinalIgnoreCase)
                         .Replace("->", ",")
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var src = parts[0];
                var dst = parts[1];
                return $"s.`{src}` AS `{dst}`";
            }
        }
        // COMPUTE Target = expr
        if (t.StartsWith("COMPUTE", StringComparison.OrdinalIgnoreCase))
        {
            var expr = t.Substring(7).Trim();
            var idx = expr.IndexOf('=');
            if (idx > 0)
            {
                var dst = expr.Substring(0, idx).Trim();
                var rhs = expr.Substring(idx + 1).Trim();
                rhs = rhs.Replace("+", "+").Replace("-", "-").Replace("*", "*").Replace("/", "/");
                return $"({rhs}) AS `{dst}`";
            }
        }
        // IF condition THEN target = value ELSE target = value
        if (t.StartsWith("IF", StringComparison.OrdinalIgnoreCase))
        {
            var condPart = t.Substring(2).Trim();
            var thenIdx = condPart.IndexOf("THEN", StringComparison.OrdinalIgnoreCase);
            var elseIdx = condPart.IndexOf("ELSE", StringComparison.OrdinalIgnoreCase);
            if (thenIdx > 0 && elseIdx > thenIdx)
            {
                var cond = condPart.Substring(0, thenIdx).Trim();
                var thenAssign = condPart.Substring(thenIdx + 4, elseIdx - (thenIdx + 4)).Trim();
                var elseAssign = condPart.Substring(elseIdx + 4).Trim();
                var (dst, thenVal) = ParseAssign(thenAssign);
                var (_, elseVal) = ParseAssign(elseAssign);
                return $"(CASE WHEN {TranslateCondition(cond)} THEN {thenVal} ELSE {elseVal} END) AS `{dst}`";
            }
        }
        // DATE8 yyyyMMdd to DATE
        if (t.StartsWith("DATE8", StringComparison.OrdinalIgnoreCase))
        {
            // DATE8 Target = Field
            var expr = t.Substring(5).Trim();
            var idx = expr.IndexOf('=');
            if (idx > 0)
            {
                var dst = expr.Substring(0, idx).Trim();
                var src = expr.Substring(idx + 1).Trim();
                return $"STR_TO_DATE(s.`{src}`, '%Y%m%d') AS `{dst}`";
            }
        }
        // COMP3 normalize: returns DECIMAL same field name with _DEC suffix
        if (t.StartsWith("COMP3", StringComparison.OrdinalIgnoreCase))
        {
            var name = t.Substring(5).Trim();
            return $"s.`{name}` AS `{name}_DEC`"; // placeholder
        }
        return string.Empty;
    }

    private static (string dst, string val) ParseAssign(string s)
    {
        var idx = s.IndexOf('=');
        if (idx > 0) return (s.Substring(0, idx).Trim(), s.Substring(idx + 1).Trim());
        return ("", s);
    }

    private static string TranslateCondition(string cond)
    {
        // Very naive translation: replace '=' with '=', AND/OR preserved
        return cond.Replace("=", "=");
    }
}
