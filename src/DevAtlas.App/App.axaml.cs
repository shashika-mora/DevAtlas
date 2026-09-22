using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DevAtlas.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DevAtlas.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DevAtlas");
        services.AddSingleton(new SqliteWorkspaceStore(Path.Combine(dataDirectory, "devatlas.db")));
        services.AddSingleton<FileSystemProjectDiscovery>();
        services.AddSingleton<ProcessGitReader>();
        services.AddSingleton<MainWindowViewModel>();
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow { DataContext = Services.GetRequiredService<MainWindowViewModel>() };
        base.OnFrameworkInitializationCompleted();
    }
}
