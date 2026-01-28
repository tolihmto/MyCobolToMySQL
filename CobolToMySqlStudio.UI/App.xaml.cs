using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CobolToMySqlStudio.Application.Interfaces;
using CobolToMySqlStudio.Application.Services;
using CobolToMySqlStudio.Infrastructure;

namespace CobolToMySqlStudio.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((ctx, config) =>
            {
                // Ensure we read appsettings.json from the executable folder
                var basePath = AppContext.BaseDirectory;
                config.SetBasePath(basePath);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                // Allow environment overrides
                config.AddEnvironmentVariables(prefix: "COBOLSTUDIO_");
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
            })
            .ConfigureServices((ctx, services) =>
            {
                // Application services
                services.AddSingleton<ICopybookParser, CopybookParser>();
                services.AddSingleton<ILayoutCalculator, LayoutCalculator>();
                services.AddSingleton<ISqlGenerator, SqlGenerator>();
                services.AddSingleton<IImportService, ImportService>();
                services.AddSingleton<ITransformEngine, TransformEngine>();

                // Infrastructure
                services.AddSingleton<IDbExecutor>(sp =>
                {
                    var cfg = sp.GetRequiredService<IConfiguration>();
                    // Resolve connection string with fallbacks
                    string? cs =
                        cfg.GetConnectionString("MySql")
                        ?? cfg["ConnectionStrings:MySql"]
                        ?? cfg["ConnectionStrings:MySqlThomas"]
                        ?? cfg["ConnectionStrings:MySqlLocal"]
                        ?? cfg["ConnectionStrings:MySqlDocker"]
                        ?? Environment.GetEnvironmentVariable("COBOLSTUDIO_MYSQL");

                    if (string.IsNullOrWhiteSpace(cs))
                    {
                        MessageBox.Show(
                            "No MySQL connection string found. Please edit CobolToMySqlStudio.UI/appsettings.json (key: ConnectionStrings:MySql) or set COBOLSTUDIO_MYSQL.",
                            "CobolToMySql Studio",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        cs = string.Empty;
                    }
                    return new MySqlDbExecutor(cs);
                });

                // ViewModels
                services.AddSingleton<ViewModels.MainViewModel>();

                // Windows
                services.AddSingleton<MainWindow>(sp =>
                {
                    var vm = sp.GetRequiredService<ViewModels.MainViewModel>();
                    return new MainWindow { DataContext = vm };
                });
            })
            .Build();

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }
}

