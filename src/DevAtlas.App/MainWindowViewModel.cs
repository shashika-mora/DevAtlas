using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAtlas.Domain;
using DevAtlas.Infrastructure;

namespace DevAtlas.App;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly SqliteWorkspaceStore _store;
    private readonly FileSystemProjectDiscovery _discovery;
    private readonly ProcessGitReader _git;
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;

    public ObservableCollection<WorkspaceRoot> Roots { get; } = [];
    public ObservableCollection<ProjectCard> Projects { get; } = [];
    public IEnumerable<ProjectCard> FilteredProjects =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Projects
            : Projects.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || p.Path.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
    public int ModifiedCount => Projects.Count(p => p.HasChanges);
    public bool HasNoProjects => Projects.Count == 0;

    public MainWindowViewModel(SqliteWorkspaceStore store, FileSystemProjectDiscovery discovery, ProcessGitReader git)
    {
        _store = store;
        _discovery = discovery;
        _git = git;
        _ = InitializeAsync();
    }

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredProjects));

    [RelayCommand]
    private async Task RescanAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await RescanCoreAsync();
        }
        catch (IOException ex)
        {
            StatusMessage = $"Rescan failed: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = $"Rescan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch (IOException ex)
        {
            StatusMessage = $"Startup failed: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = $"Startup failed: {ex.Message}";
        }
    }

    private async Task RescanCoreAsync()
    {
        await _scanLock.WaitAsync();
        StatusMessage = "Scanning workspace roots…";
        try
        {
            Projects.Clear();
            foreach (var root in Roots)
            {
                await foreach (var project in _discovery.DiscoverAsync([root]))
                {
                    try
                    {
                        var git = await _git.ReadAsync(project.Path);
                        var enriched = project with
                        {
                            CurrentBranch = git.Branch,
                            ChangedFileCount = git.ChangedFileCount,
                            LastWorkedAt = DateTimeOffset.Now
                        };
                        await _store.SaveAsync(enriched);
                        Projects.Add(new ProjectCard(enriched));
                    }
                    catch (InvalidOperationException ex)
                    {
                        StatusMessage = $"Could not inspect {project.Name}: {ex.Message}";
                    }
                }
            }
            OnPropertyChanged(nameof(ModifiedCount));
            OnPropertyChanged(nameof(FilteredProjects));
            OnPropertyChanged(nameof(HasNoProjects));
            StatusMessage = $"{Projects.Count} projects discovered.";
        }
        finally
        {
            _scanLock.Release();
        }
    }

    public async Task AddRootAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        var root = new WorkspaceRoot(Guid.NewGuid(), Path.GetFullPath(path));
        if (Roots.Any(existing => string.Equals(existing.Path, root.Path, StringComparison.OrdinalIgnoreCase)))
            return;
        await _store.AddAsync(root);
        Roots.Add(root);
        IsBusy = true;
        try { await RescanCoreAsync(); }
        finally { IsBusy = false; }
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            foreach (var root in await _store.GetAllAsync()) Roots.Add(root);
            var saved = await ((DevAtlas.Application.IProjectStore)_store).GetProjectsAsync();
            foreach (var project in saved) Projects.Add(new ProjectCard(project));
            OnPropertyChanged(nameof(HasNoProjects));
        }
        finally { IsBusy = false; }
    }
}

public sealed class ProjectCard(Project project)
{
    public string Name => project.Name;
    public string Path => project.Path;
    public string ProjectType => project.ProjectType;
    public string Language => project.Language;
    public string? CurrentBranch => project.CurrentBranch;
    public int ChangedFileCount => project.ChangedFileCount;
    public bool HasChanges => ChangedFileCount > 0;
}
