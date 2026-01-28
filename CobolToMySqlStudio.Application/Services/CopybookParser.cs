using System.Text;
using System.Text.RegularExpressions;
using CobolToMySqlStudio.Application.Interfaces;
using CobolToMySqlStudio.Domain.Models;

namespace CobolToMySqlStudio.Application.Services;

public class CopybookParser : ICopybookParser
{
    private static readonly Regex LineRegex = new(
        @"^(?<level>\d{2})\s+(?<name>[A-Z0-9-]+)(\s+REDEFINES\s+(?<redefines>[A-Z0-9-]+))?(\s+OCCURS\s+(?<occurs>\d+)\s+TIMES)?(\s+PIC(TURE)?\s+(?<pic>[^\s]+))?(\s+USAGE\s+(?<usage>COMP-3|COMP|BINARY))?(\s+SIGN\s+(?<sign>LEADING|TRAILING))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public CopybookParseResult Parse(string text)
    {
        var root = new CopybookNode { Name = "ROOT", Level = 0, IsGroup = true };
        var stack = new Stack<CopybookNode>();
        stack.Push(root);

        foreach (var rawLine in Normalize(text))
        {
            var line = StripSequenceAndComments(rawLine);
            if (string.IsNullOrWhiteSpace(line)) continue;
            var m = LineRegex.Match(line.Trim());
            if (!m.Success) continue;

            int level = int.Parse(m.Groups["level"].Value);
            var node = new CopybookNode
            {
                Level = level,
                Name = m.Groups["name"].Value,
                Redefines = m.Groups["redefines"].Success ? m.Groups["redefines"].Value : null,
                Occurs = m.Groups["occurs"].Success ? int.Parse(m.Groups["occurs"].Value) : null,
                Picture = m.Groups["pic"].Success ? m.Groups["pic"].Value : null,
                Usage = ParseUsage(m.Groups["usage"].Value),
                IsSigned = m.Groups["sign"].Success,
                IsGroup = !m.Groups["pic"].Success
            };

            while (stack.Peek().Level >= level) stack.Pop();
            var parent = stack.Peek();
            node.Parent = parent;
            parent.Children.Add(node);
            stack.Push(node);
        }

        return new CopybookParseResult { Root = root };
    }

    private static IEnumerable<string> Normalize(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
    }

    private static string StripSequenceAndComments(string line)
    {
        if (line.Length > 6) line = line.Substring(6); // drop sequence area columns 1-6
        var starIdx = line.IndexOf("*", StringComparison.Ordinal);
        if (starIdx >= 0 && (starIdx == 0 || char.IsWhiteSpace(line[starIdx - 1])))
            return string.Empty; // full-line comment
        var quote = false;
        var sb = new StringBuilder();
        foreach (var ch in line)
        {
            if (ch == '\'') quote = !quote;
            if (!quote && ch == '\n') break;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static UsageType ParseUsage(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return UsageType.Display;
        value = value.ToUpperInvariant();
        return value switch
        {
            "COMP" => UsageType.Comp,
            "COMP-3" => UsageType.Comp3,
            "BINARY" => UsageType.Binary,
            _ => UsageType.Display
        };
    }
}
