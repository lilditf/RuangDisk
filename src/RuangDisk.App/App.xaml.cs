using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RuangDisk.App.Services;
using RuangDisk.App.ViewModels;
using RuangDisk.Core.Contracts;
using RuangDisk.Infrastructure.Actions;
using RuangDisk.Infrastructure.Export;
using RuangDisk.Infrastructure.Persistence;
using RuangDisk.Infrastructure.Scanning;
using RuangDisk.Infrastructure.Services;

namespace RuangDisk.App;

/// <summary>
/// Composition Root dan lifecycle aplikasi RuangDisk.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        Services = serviceCollection.BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core & Infrastructure Services
        services.AddSingleton<IFileSystemScanner, FileSystemScanner>();
        services.AddSingleton<IDriveService, DriveService>();
        services.AddSingleton<ISnapshotStore, SnapshotStore>();
        services.AddSingleton<IFileActionService, WindowsFileActionService>();
        services.AddSingleton<IExportService, ExportService>();

        // App Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<TreemapLayoutService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }
}
