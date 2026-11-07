using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DevAtlas.Application;
using DevAtlas.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DevAtlas.App;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // An explicit override supports isolated smoke tests and portable data profiles.
        var dataDirectory = Environment.GetEnvironmentVariable("DEVATLAS_DATA_DIRECTORY")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DevAtlas");
        var services = new ServiceCollection();
        services.AddSingleton<IAppLog>(new FileAppLog(Path.Combine(dataDirectory, "diagnostics.log")));
        services.AddSingleton<SqliteWorkspaceStore>(_ => new SqliteWorkspaceStore(Path.Combine(dataDirectory, "devatlas.db")));
        services.AddSingleton<IWorkspaceRootStore>(provider => provider.GetRequiredService<SqliteWorkspaceStore>());
        services.AddSingleton<IProjectStore>(provider => provider.GetRequiredService<SqliteWorkspaceStore>());
        services.AddSingleton<IProjectDiscovery, FileSystemProjectDiscovery>();
        services.AddSingleton<IGitReader>(_ => new ProcessGitReader());
        services.AddSingleton<IWorkspaceScanner, WorkspaceScanner>();
        services.AddSingleton<MainWindowViewModel>();
        var provider = services.BuildServiceProvider();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = provider.GetRequiredService<MainWindowViewModel>() };
            desktop.Exit += (_, _) => provider.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
