using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAtlas.Application;
using DevAtlas.Domain;

namespace DevAtlas.App;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IWorkspaceRootStore _roots;
    private readonly IProjectStore _projects;
    private readonly IWorkspaceScanner _scanner;
    private readonly IGitReader _git;
    private readonly IAppLog _log;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _scanCancellation;
    private CancellationTokenSource? _detailsCancellation;
    private bool _closed;
    private bool _updatingProjects;

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private bool favoritesOnly;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = "Choose a workspace root to get started.";
    [ObservableProperty] private ProjectCard? selectedProject;
    [ObservableProperty] private GitSnapshot? selectedGit;
    [ObservableProperty] private string detailsMessage = "Select a project to inspect its Git information.";
    [ObservableProperty] private bool isLoadingDetails;

    public ObservableCollection<WorkspaceRoot> Roots { get; } = [];
    public ObservableCollection<ProjectCard> Projects { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];
    public IEnumerable<ProjectCard> FilteredProjects => Projects.Where(p =>
        (!FavoritesOnly || p.IsFavorite) && (string.IsNullOrWhiteSpace(SearchText)
        || p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
        || p.Path.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
        || p.ProjectType.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
    public int ModifiedCount => Projects.Count(p => p.HasChanges);
    public int VisibleCount => FilteredProjects.Count();
    public bool HasNoProjects => Projects.Count == 0;
    public bool CanEdit => !IsBusy;
    public bool HasSelection => SelectedProject != null;
    public string VersionText => $"DevAtlas {typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3)} · Windows preview";

    public MainWindowViewModel(IWorkspaceRootStore roots, IProjectStore projects, IWorkspaceScanner scanner, IGitReader git, IAppLog log)
    {
        _roots = roots; _projects = projects; _scanner = scanner; _git = git; _log = log;
        Projects.CollectionChanged += (_, _) => { if (!_updatingProjects) NotifyProjects(); };
    }

    partial void OnSearchTextChanged(string value) => NotifyProjects();
    partial void OnFavoritesOnlyChanged(bool value) => NotifyProjects();
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));
    partial void OnSelectedProjectChanged(ProjectCard? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        _detailsCancellation?.Cancel();
        _detailsCancellation?.Dispose();
        _detailsCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _ = LoadDetailsAsync(value, _detailsCancellation.Token);
    }

    public async Task InitializeAsync()
    {
        await ExecuteAsync(async token =>
        {
            await ReloadRootsAsync(token);
            SetProjects(await _projects.GetProjectsAsync(token));
            StatusMessage = $"Loaded {Projects.Count} saved projects. Rescan to refresh Git information.";
        }, "Startup");
    }

    [RelayCommand]
    private async Task RescanAsync()
    {
        await ExecuteAsync(async token =>
        {
            _scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            try
            {
                var progress = new Progress<string>(message => { if (!_closed && IsBusy) StatusMessage = message; });
                StatusMessage = "Scanning workspace roots…";
                var result = await _scanner.ScanAsync(progress, _scanCancellation.Token);
                SetProjects(result.Projects);
                Warnings.Clear();
                foreach (var warning in result.Warnings) Warnings.Add(warning);
                StatusMessage = $"{Projects.Count} projects · {Warnings.Count} warnings · scan complete.";
            }
            finally { _scanCancellation.Dispose(); _scanCancellation = null; }
        }, "Scan");
    }

    [RelayCommand]
    private void CancelScan() => _scanCancellation?.Cancel();

    public async Task AddRootAsync(string path)
    {
        var added = false;
        await ExecuteAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) throw new IOException("The chosen folder is unavailable.");
            var normalized = ProjectPaths.Normalize(path);
            if (Roots.Any(root => ProjectPaths.Comparer.Equals(ProjectPaths.Normalize(root.Path), normalized)))
            {
                StatusMessage = "That workspace root is already configured.";
                return;
            }
            await _roots.AddAsync(new WorkspaceRoot(Guid.NewGuid(), normalized), token);
            added = true;
            await ReloadRootsAsync(token);
            StatusMessage = "Workspace root added. Select Rescan to discover its projects.";
        }, "Add root");
        if (added) await RescanAsync();
    }

    [RelayCommand]
    private async Task RemoveRootAsync(WorkspaceRoot? root)
    {
        if (root == null) return;
        await ExecuteAsync(async token =>
        {
            await _roots.RemoveAsync(root.Id, token);
            await ReloadRootsAsync(token);
            await _projects.ApplyScanAsync([], [], token);
            SetProjects(await _projects.GetProjectsAsync(token));
            StatusMessage = "Root removed from DevAtlas. Files on disk are unchanged.";
        }, "Remove root");
    }

    [RelayCommand]
    private async Task ToggleRootAsync(WorkspaceRoot? root)
    {
        if (root == null) return;
        await ExecuteAsync(async token =>
        {
            await _roots.SetIgnoredAsync(root.Id, !root.IsIgnored, token);
            await ReloadRootsAsync(token);
            await _projects.ApplyScanAsync([], [], token);
            SetProjects(await _projects.GetProjectsAsync(token));
            StatusMessage = root.IsIgnored ? "Root enabled. Rescan to refresh its projects." : "Root ignored. Files and saved favorites are retained.";
        }, "Update root");
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedProject == null) return;
        var selected = SelectedProject;
        await ExecuteAsync(async token =>
        {
            await _projects.SaveAsync(selected.Project with { IsFavorite = !selected.IsFavorite }, token);
            SetProjects(await _projects.GetProjectsAsync(token));
            StatusMessage = "Favorite updated.";
        }, "Favorite");
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> action, string operation)
    {
        if (IsBusy || _closed) return;
        IsBusy = true;
        try { await action(_lifetime.Token); }
        catch (OperationCanceledException) { StatusMessage = "Scan cancelled. The previous saved project list was retained."; }
        catch (Exception ex)
        {
            _log.Write(operation + " failed", ex);
            StatusMessage = $"{operation} failed. Check folder access and available disk space, then retry. Local diagnostics record the error type.";
        }
        finally { IsBusy = false; }
    }

    private async Task LoadDetailsAsync(ProjectCard? card, CancellationToken token)
    {
        SelectedGit = null;
        IsLoadingDetails = card != null;
        DetailsMessage = card == null ? "Select a project to inspect its Git information." : "Reading Git information…";
        try
        {
            if (card == null) return;
            var snapshot = await _git.ReadAsync(card.Path, token);
            if (token.IsCancellationRequested || SelectedProject != card) return;
            SelectedGit = snapshot;
            DetailsMessage = snapshot.IsRepository ? $"{snapshot.ChangedFileCount} changed files · {snapshot.Branch}" : "No Git repository at this project folder.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Write("Project details unavailable", ex);
            if (!token.IsCancellationRequested && SelectedProject == card)
                DetailsMessage = "Git details unavailable. Check that Git is installed and the repository is accessible.";
        }
        finally { if (SelectedProject == card) IsLoadingDetails = false; }
    }

    private async Task ReloadRootsAsync(CancellationToken token)
    {
        var roots = await _roots.GetAllAsync(token);
        Roots.Clear();
        foreach (var root in roots) Roots.Add(root);
    }

    private void SetProjects(IReadOnlyList<Project> projects)
    {
        var selectedPath = SelectedProject?.Path;
        _updatingProjects = true;
        Projects.Clear();
        foreach (var project in projects) Projects.Add(new ProjectCard(project));
        _updatingProjects = false;
        NotifyProjects();
        SelectedProject = Projects.FirstOrDefault(p => ProjectPaths.Comparer.Equals(p.Path, selectedPath));
    }

    private void NotifyProjects()
    {
        OnPropertyChanged(nameof(FilteredProjects));
        OnPropertyChanged(nameof(ModifiedCount));
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(HasNoProjects));
    }

    public void ReportError(string message) => StatusMessage = message;
    public void Close()
    {
        _closed = true;
        _lifetime.Cancel();
        _detailsCancellation?.Cancel();
        _scanCancellation?.Cancel();
    }
}

public sealed class ProjectCard(Project project)
{
    public Project Project { get; } = project;
    public string Name => Project.Name;
    public string Path => Project.Path;
    public string ProjectType => Project.ProjectType;
    public string Language => Project.Language;
    public string? CurrentBranch => Project.CurrentBranch;
    public int ChangedFileCount => Project.ChangedFileCount;
    public bool HasChanges => ChangedFileCount > 0;
    public bool IsFavorite => Project.IsFavorite;
    public string FavoriteLabel => IsFavorite ? "Remove favorite" : "Add favorite";
    public string? GitError => Project.GitError;
    public string LastScanned => Project.LastScannedAt?.ToLocalTime().ToString("g") ?? "Not yet scanned";
}
