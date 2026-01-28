using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO;
using System.Data;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CobolToMySqlStudio.Application.Interfaces;
using CobolToMySqlStudio.Domain.Models;
using Microsoft.Win32;
using CobolToMySqlStudio.Infrastructure;
using CobolToMySqlStudio.UI;

namespace CobolToMySqlStudio.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ICopybookParser _parser;
    private readonly ILayoutCalculator _layout;
    private readonly ISqlGenerator _sqlGen;
    private readonly IImportService _import;
    private readonly ITransformEngine _transform;
    private readonly IDbExecutor _db;

    public MainViewModel(ICopybookParser parser,
                         ILayoutCalculator layout,
                         ISqlGenerator sqlGen,
                         IImportService import,
                         ITransformEngine transform,
                         IDbExecutor db)
    {
        _parser = parser;
        _layout = layout;
        _sqlGen = sqlGen;
        _import = import;
        _transform = transform;
        _db = db;

        RootNodes = new ObservableCollection<CopybookNode>();
        StagingColumns = new ObservableCollection<string>();
        OpenCopybookCommand = new AsyncRelayCommand(OpenCopybookAsync);
        ImportXmlCommand = new AsyncRelayCommand(ImportXmlAsync);
        PreviewDdlCommand = new RelayCommand(PreviewDdl, () => _root != null && !string.IsNullOrWhiteSpace(StagingTableName));
        // Allow Apply DDL even if preview is empty; we'll auto-generate
        ApplyDdlCommand = new AsyncRelayCommand(ApplyDdlAsync, () => _root != null && !string.IsNullOrWhiteSpace(StagingTableName));
        ChooseDataFileCommand = new RelayCommand(ChooseDataFile);
        RunImportCommand = new AsyncRelayCommand(RunImportAsync, () => _root != null && !string.IsNullOrWhiteSpace(DataFilePath) && !string.IsNullOrWhiteSpace(StagingTableName));
        GenerateSqlCommand = new RelayCommand(GenerateSql, () => _root != null && !string.IsNullOrWhiteSpace(TransformDsl));
        ApplyTransformCommand = new AsyncRelayCommand(ApplyTransformAsync, () => !string.IsNullOrWhiteSpace(GeneratedSql));
        PreviewResultsCommand = new AsyncRelayCommand(PreviewResultsAsync);
        OpenConnectionDialogCommand = new RelayCommand(OpenConnectionDialog);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
    }

    private CopybookNode? _root;

    public ObservableCollection<CopybookNode> RootNodes { get; }
    public ObservableCollection<string> StagingColumns { get; }

    private string _statusText = "Ready";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    private string _copybookPath = string.Empty;
    public string CopybookPath { get => _copybookPath; set => SetProperty(ref _copybookPath, value); }

    private bool _showFiller = true;
    public bool ShowFiller { get => _showFiller; set => SetProperty(ref _showFiller, value); }

    private string _stagingTableName = "staging_records";
    public string StagingTableName { get => _stagingTableName; set { SetProperty(ref _stagingTableName, value); (PreviewDdlCommand as RelayCommand)?.NotifyCanExecuteChanged(); (RunImportCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged(); } }

    private string _ddlPreview = string.Empty;
    public string DdlPreview { get => _ddlPreview; set => SetProperty(ref _ddlPreview, value); }

    private string _dataFilePath = string.Empty;
    public string DataFilePath { get => _dataFilePath; set { SetProperty(ref _dataFilePath, value); (RunImportCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged(); } }

    private double _importProgress;
    public double ImportProgress { get => _importProgress; set => SetProperty(ref _importProgress, value); }

    public ObservableCollection<string> ImportErrors { get; } = new();

    private string _transformDsl = "-- Examples:\nMOVE SRC_FIELD -> DST_FIELD\nCOMPUTE TOTAL = A + B\nIF A = 1 THEN FLAG = 'Y' ELSE FLAG = 'N'\nDATE8 BIRTHDATE = BIRTH_YYYYMMDD\nCOMP3 AMOUNT";
    public string TransformDsl { get => _transformDsl; set { SetProperty(ref _transformDsl, value); (GenerateSqlCommand as RelayCommand)?.NotifyCanExecuteChanged(); } }

    private string _generatedSql = string.Empty;
    public string GeneratedSql { get => _generatedSql; set { SetProperty(ref _generatedSql, value); (ApplyTransformCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged(); } }

    private string _logs = string.Empty;
    public string Logs { get => _logs; set => SetProperty(ref _logs, value); }

    public IAsyncRelayCommand OpenCopybookCommand { get; }
    public IAsyncRelayCommand ImportXmlCommand { get; }
    public IRelayCommand PreviewDdlCommand { get; }
    public IAsyncRelayCommand ApplyDdlCommand { get; }
    public IRelayCommand ChooseDataFileCommand { get; }
    public IAsyncRelayCommand RunImportCommand { get; }
    public IRelayCommand GenerateSqlCommand { get; }
    public IAsyncRelayCommand ApplyTransformCommand { get; }
    public IAsyncRelayCommand PreviewResultsCommand { get; }
    public IRelayCommand OpenConnectionDialogCommand { get; }
    public IAsyncRelayCommand TestConnectionCommand { get; }

    private async Task OpenCopybookAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Copybook (*.cpy;*.txt)|*.cpy;*.txt|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            CopybookPath = dlg.FileName;
            var text = await File.ReadAllTextAsync(dlg.FileName);
            var result = _parser.Parse(text);
            _layout.ComputeOffsets(result.Root);
            _root = result.Root;
            RootNodes.Clear();
            foreach (var child in _root.Children) RootNodes.Add(child);
            StagingColumns.Clear();
            foreach (var leaf in GetLeaves(_root).Where(l => !l.IsFiller))
            {
                StagingColumns.Add(leaf.Name.Replace('-', '_'));
            }
            StatusText = $"Parsed copybook. Total length ~ {_layout.GetTotalLength(result.Root)} bytes";
            if (string.IsNullOrWhiteSpace(StagingTableName)) StagingTableName = "staging_" + Path.GetFileNameWithoutExtension(dlg.SafeFileName).ToLowerInvariant();
            (PreviewDdlCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (RunImportCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (GenerateSqlCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (ApplyDdlCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();

            // On dataset change, drop the previous curated view so Preview falls back to staging
            try
            {
                await _db.ExecuteNonQueryAsync("DROP VIEW IF EXISTS `curated_view`");
                AppendLog("Dropped existing curated_view to switch dataset.");
            }
            catch (Exception ex)
            {
                AppendLog($"Drop view warning: {ex.Message}");
            }
        }
    }

    private async Task ImportXmlAsync()
    {
        // Placeholder for future XML importer (CB2XML style). For now, show file picker and log.
        var dlg = new OpenFileDialog { Filter = "XML (*.xml)|*.xml|All files (*.*)|*.*" };
        if (dlg.ShowDialog() == true)
        {
            AppendLog($"XML import is not yet implemented. Selected: {dlg.FileName}");
        }
        await Task.CompletedTask;
    }

    private void PreviewDdl()
    {
        if (_root == null) return;
        DdlPreview = _sqlGen.GenerateStagingTableDdl(StagingTableName, _root);
    }

    private async Task ApplyDdlAsync()
    {
        if (_root == null || string.IsNullOrWhiteSpace(StagingTableName)) return;
        if (string.IsNullOrWhiteSpace(DdlPreview))
        {
            DdlPreview = _sqlGen.GenerateStagingTableDdl(StagingTableName, _root);
        }
        int n = await _db.ExecuteNonQueryAsync(DdlPreview);
        StatusText = $"DDL applied (result {n}).";
    }

    private void ChooseDataFile()
    {
        var dlg = new OpenFileDialog { Filter = "Data (*.dat;*.txt;*.csv)|*.dat;*.txt;*.csv|All files (*.*)|*.*" };
        if (dlg.ShowDialog() == true)
        {
            DataFilePath = dlg.FileName;
        }
    }

    private async Task RunImportAsync()
    {
        if (_root == null || string.IsNullOrWhiteSpace(DataFilePath)) return;
        StatusText = "Importing...";
        try
        {
            await _import.ImportWithAstAsync(DataFilePath, StagingTableName, _root);
            StatusText = "Import completed.";
        }
        catch (Exception ex)
        {
            AppendLog($"Import error: {ex.Message}");
            ImportErrors.Add(ex.Message);
            StatusText = "Import failed.";
        }
    }

    private void GenerateSql()
    {
        if (string.IsNullOrWhiteSpace(StagingTableName)) return;
        var dsl = TransformDsl;
        if (string.IsNullOrWhiteSpace(dsl) || dsl.Contains("SRC_FIELD", StringComparison.OrdinalIgnoreCase))
        {
            dsl = BuildDefaultDsl();
            TransformDsl = dsl;
        }
        GeneratedSql = _transform.GenerateSql(StagingTableName, "curated_view", dsl);
    }

    private string BuildDefaultDsl()
    {
        // Build a simple DSL mapping existing leaf fields to themselves, with a couple of common conveniences
        var lines = new List<string>();
        if (_root != null)
        {
            var leaves = GetLeaves(_root).Where(l => !l.IsFiller).ToList();
            foreach (var f in leaves)
            {
                var name = f.Name.Replace('-', '_');
                lines.Add($"MOVE {name} -> {name}");
            }
            var birth = leaves.FirstOrDefault(x => x.Name.Equals("BIRTH-YYYYMMDD", StringComparison.OrdinalIgnoreCase));
            if (birth != null)
            {
                lines.Add($"DATE8 BIRTHDATE = BIRTH_YYYYMMDD");
            }
        }
        return string.Join('\n', lines);
    }

    private static IEnumerable<CopybookNode> GetLeaves(CopybookNode node)
    {
        foreach (var c in node.Children)
        {
            if (c.IsGroup)
            {
                foreach (var g in GetLeaves(c)) yield return g;
            }
            else yield return c;
        }
    }

    private async Task ApplyTransformAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(GeneratedSql))
            {
                GenerateSql();
            }
            if (string.IsNullOrWhiteSpace(GeneratedSql))
            {
                StatusText = "Nothing to apply. Generate SQL first.";
                return;
            }
            int n = await _db.ExecuteNonQueryAsync(GeneratedSql);
            StatusText = $"Transformation applied (result {n}).";
        }
        catch (Exception ex)
        {
            AppendLog($"Apply error: {ex.Message}");
            StatusText = "Apply failed. See Logs.";
        }
        finally
        {
            (ApplyTransformCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }
    }

    private DataView? _previewRows;
    public DataView? PreviewRows { get => _previewRows; set => SetProperty(ref _previewRows, value); }

    private async Task PreviewResultsAsync()
    {
        try
        {
            var rows = await _db.QueryAsync("SELECT * FROM `curated_view` LIMIT 200");
            var table = new DataTable();
            if (rows.Count > 0)
            {
                foreach (var col in rows[0].Keys)
                {
                    table.Columns.Add(col);
                }
                foreach (var r in rows)
                {
                    var dr = table.NewRow();
                    foreach (var kv in r)
                    {
                        dr[kv.Key] = kv.Value ?? DBNull.Value;
                    }
                    table.Rows.Add(dr);
                }
                PreviewRows = table.DefaultView;
                StatusText = $"Loaded {rows.Count} rows from curated_view.";
                return;
            }

            // Fallback: try staging table if curated_view is empty or missing
            if (!string.IsNullOrWhiteSpace(StagingTableName))
            {
                var stgRows = await _db.QueryAsync($"SELECT * FROM `{StagingTableName}` LIMIT 200");
                var stgTable = new DataTable();
                if (stgRows.Count > 0)
                {
                    foreach (var col in stgRows[0].Keys) stgTable.Columns.Add(col);
                    foreach (var r in stgRows)
                    {
                        var dr = stgTable.NewRow();
                        foreach (var kv in r) dr[kv.Key] = kv.Value ?? DBNull.Value;
                        stgTable.Rows.Add(dr);
                    }
                }
                PreviewRows = stgTable.DefaultView;
                StatusText = $"Loaded {stgRows.Count} rows from {StagingTableName}.";
                return;
            }

            PreviewRows = table.DefaultView; // empty
            StatusText = "No rows to preview.";
        }
        catch (Exception ex)
        {
            AppendLog($"Preview error: {ex.Message}");
        }
    }

    private void AppendLog(string text)
    {
        Logs += $"[{DateTime.Now:HH:mm:ss}] {text}\r\n";
    }

    private void OpenConnectionDialog()
    {
        var dlg = new ConnectionDialog();
        var vm = new ConnectionViewModel();
        if (_db is MySqlDbExecutor mysqlExisting)
        {
            vm.PrefillFromConnectionString(mysqlExisting.GetConnectionString());
        }
        vm.CloseRequested += (ok, connStr) =>
        {
            if (ok && !string.IsNullOrWhiteSpace(connStr))
            {
                ApplyConnectionString(connStr);
                StatusText = "Connection updated.";
            }
            dlg.DialogResult = ok;
            dlg.Close();
        };
        dlg.DataContext = vm;
        dlg.Owner = System.Windows.Application.Current?.MainWindow;
        dlg.ShowDialog();
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            var rows = await _db.QueryAsync("SELECT 1");
            StatusText = rows.Count > 0 ? "Connection OK" : "Connection test returned no rows";
        }
        catch (Exception ex)
        {
            AppendLog($"Test connection error: {ex.Message}");
            StatusText = "Connection failed. See Logs.";
        }
    }

    private void ApplyConnectionString(string cs)
    {
        if (_db is MySqlDbExecutor mysql)
        {
            mysql.ApplyConnectionString(cs);
        }
    }
}
