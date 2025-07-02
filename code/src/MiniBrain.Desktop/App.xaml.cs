using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Configuration;
using System.Data;
using System.Windows;

namespace MiniBrain.Desktop;

public partial class App : Application
{
    private IHost? _host;
    
    // Static logger factory that can be used throughout the application
    public static ILoggerFactory? LoggerFactory { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddHttpClient();
                services.AddSingleton<MainWindow>();
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Trace);
                });
            })
            .Build();
            
        // Set up the default logger factory
        var factory = _host.Services.GetRequiredService<ILoggerFactory>();
        LoggerFactory = factory;

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}

