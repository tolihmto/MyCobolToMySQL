using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using CobolToMySqlStudio.Application.Interfaces;
using CobolToMySqlStudio.Domain.Models;

namespace CobolToMySqlStudio.Application.Services;

public class ImportService : IImportService
{
    private readonly IDbExecutor _db;
    private readonly ICopybookParser _parser;
    private readonly ILayoutCalculator _layout;

    public ImportService(IDbExecutor db, ICopybookParser parser, ILayoutCalculator layout)
    {
        _db = db;
        _parser = parser;
        _layout = layout;
    }

    public async Task ImportAsync(string filePath, string tableName, CancellationToken ct = default)
    {
        // Expect an already applied staging table; perform simple fixed-width import using computed offsets
        var text = await File.ReadAllTextAsync(filePath, ct);
        var parse = _parser.Parse(text: ""); // Parser not used here directly; in real flow, pass AST from UI
        // In this minimal stub, we simply throw to indicate UI should pass a prepared AST/service overload
        throw new NotSupportedException("ImportService.ImportAsync minimal stub: UI must pass parsed copybook and field map. To keep solution runnable, actual ETL is invoked from UI using a helper.");
    }

    // Helper used by UI to import with given AST
    public async Task<int> ImportWithAstAsync(string dataFilePath, string tableName, CopybookNode root, CancellationToken ct = default)
    {
        _layout.ComputeOffsets(root);
        var leaves = EnumerateLeaves(root).Where(l => !l.IsFiller).ToList();
        var rows = new List<IDictionary<string, object?>>();
        await foreach (var line in ReadLinesAsync(dataFilePath, ct))
        {
            var row = new Dictionary<string, object?>();
            foreach (var f in leaves)
            {
                var val = ExtractField(line, f);
                row[SafeName(f.Name)] = val;
            }
            row["ImportFileName"] = Path.GetFileName(dataFilePath);
            rows.Add(row);
            if (rows.Count >= 1000)
            {
                await _db.BulkInsertAsync(tableName, rows, ct);
                rows.Clear();
            }
        }
        if (rows.Count > 0)
        {
            await _db.BulkInsertAsync(tableName, rows, ct);
        }
        return 0;
    }

    private static IEnumerable<CopybookNode> EnumerateLeaves(CopybookNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.IsGroup)
            {
                foreach (var c in EnumerateLeaves(child)) yield return c;
            }
            else yield return child;
        }
    }

    private static string SafeName(string name) => name.Replace('-', '_');

    private static object? ExtractField(string record, CopybookNode f)
    {
        int len = Math.Min(f.StorageLength, Math.Max(0, record.Length - f.Offset));
        if (len <= 0) return null;
        var slice = record.Substring(f.Offset, len).Trim();
        if (string.IsNullOrEmpty(slice)) return null;
        if (f.Picture != null && f.Picture.ToUpperInvariant().Contains('9'))
        {
            if (decimal.TryParse(slice, out var dec)) return dec;
        }
        return slice;
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sr = new StreamReader(fs, Encoding.ASCII, detectEncodingFromByteOrderMarks: true);
        while (!sr.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await sr.ReadLineAsync();
            if (line == null) continue;
            if (line.Length == 0) continue; // skip empty lines (e.g., trailing newline)
            yield return line;
        }
    }
}
