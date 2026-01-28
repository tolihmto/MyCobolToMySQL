using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CobolToMySqlStudio.Infrastructure;

namespace CobolToMySqlStudio.UI.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    public event Action<bool, string?>? CloseRequested;

    private string _server = string.Empty;
    public string Server { get => _server; set { SetProperty(ref _server, value); UpdateCommands(); } }

    private string _port = "3306";
    public string Port { get => _port; set { SetProperty(ref _port, value); UpdateCommands(); } }

    private string _database = string.Empty;
    public string Database { get => _database; set { SetProperty(ref _database, value); UpdateCommands(); } }

    private string _user = string.Empty;
    public string User { get => _user; set { SetProperty(ref _user, value); UpdateCommands(); } }

    private string _password = string.Empty;
    public string Password { get => _password; set => SetProperty(ref _password, value); }

    private string _status = string.Empty;
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { SetProperty(ref _isBusy, value); UpdateCommands(); } }

    public IAsyncRelayCommand TestCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public ConnectionViewModel()
    {
        TestCommand = new AsyncRelayCommand(TestAsync, CanTest);
        SaveCommand = new RelayCommand(Save, CanSave);
        CancelCommand = new RelayCommand(Cancel);
    }

    private void UpdateCommands()
    {
        (TestCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (SaveCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private bool CanTest() => !IsBusy && !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Database) && !string.IsNullOrWhiteSpace(User);
    private bool CanSave() => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Database) && !string.IsNullOrWhiteSpace(User);

    public void PrefillFromConnectionString(string? cs)
    {
        if (string.IsNullOrWhiteSpace(cs)) return;
        try
        {
            // Very simple parse for common keys
            foreach (var part in cs.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                var k = kv[0].Trim();
                var v = kv[1].Trim();
                switch (k.ToLowerInvariant())
                {
                    case "server": Server = v; break;
                    case "port": Port = v; break;
                    case "database": Database = v; break;
                    case "uid": User = v; break;
                    case "user id": User = v; break;
                    case "pwd": Password = v; break;
                    case "password": Password = v; break;
                }
            }
        }
        catch { /* ignore parse errors */ }
    }

    private string BuildConnectionString()
    {
        var port = int.TryParse(Port, out var p) ? p : 3306;
        return $"Server={Server};Port={port};Database={Database};Uid={User};Pwd={Password};SslMode=None;";
    }

    private async Task TestAsync()
    {
        IsBusy = true;
        Status = "Testing connection...";
        try
        {
            var cs = BuildConnectionString();
            var exec = new MySqlDbExecutor(cs);
            var rows = await exec.QueryAsync("SELECT 1");
            Status = rows.Count > 0 ? "OK" : "KO";
        }
        catch (Exception ex)
        {
            Status = "KO: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Save()
    {
        CloseRequested?.Invoke(true, BuildConnectionString());
    }

    private void Cancel()
    {
        CloseRequested?.Invoke(false, null);
    }
}
