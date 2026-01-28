using CobolToMySqlStudio.Application.Interfaces;
using CobolToMySqlStudio.Domain.Models;

namespace CobolToMySqlStudio.Application.Services;

public class LayoutCalculator : ILayoutCalculator
{
    public void ComputeOffsets(CopybookNode root)
    {
        int offset = 0;
        foreach (var child in root.Children)
        {
            offset = ComputeNode(child, offset);
        }
    }

    public int GetTotalLength(CopybookNode root)
    {
        int max = 0;
        foreach (var child in root.Children)
        {
            max = ComputeNode(child, max);
        }
        return max;
    }

    private int ComputeNode(CopybookNode node, int currentOffset)
    {
        node.Offset = currentOffset;
        if (node.IsGroup)
        {
            int innerOffset = currentOffset;
            foreach (var c in node.Children)
            {
                innerOffset = ComputeNode(c, innerOffset);
            }
            int groupLen = innerOffset - currentOffset;
            node.StorageLength = (node.Occurs.HasValue ? node.Occurs.Value : 1) * groupLen;
            return currentOffset + node.StorageLength;
        }
        else
        {
            node.StorageLength = EstimateLength(node);
            if (node.Occurs.HasValue)
            {
                node.StorageLength *= node.Occurs.Value;
            }
            return currentOffset + node.StorageLength;
        }
    }

    private static int EstimateLength(CopybookNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Picture)) return 0;
        var pic = node.Picture.ToUpperInvariant();
        // Very simplified estimates
        // X(n) -> n bytes, 9(n) -> n, S9(n)V9(m) -> n+m
        int n = ExtractNumberAfter(pic, 'X');
        if (n > 0) return n;
        if (pic.Contains('V'))
        {
            var parts = pic.Split('V');
            int left = ExtractNumberAfter(parts[0], '9');
            int right = ExtractNumberAfter(parts[1], '9');
            return left + right + (node.IsSigned ? 1 : 0);
        }
        int digits = ExtractNumberAfter(pic, '9');
        if (digits > 0) return digits + (node.IsSigned ? 1 : 0);
        return 0;
    }

    private static int ExtractNumberAfter(string pic, char c)
    {
        // match like X(10) or 9(5)
        int idx = pic.IndexOf(c);
        if (idx < 0) return 0;
        int open = pic.IndexOf('(', idx);
        int close = pic.IndexOf(')', open + 1);
        if (open > 0 && close > open)
        {
            if (int.TryParse(pic.Substring(open + 1, close - open - 1), out int val)) return val;
        }
        // Support repeated symbols without parentheses e.g. XXXXX
        int count = 0;
        foreach (var ch in pic)
        {
            if (ch == c) count++;
        }
        return count;
    }
}
