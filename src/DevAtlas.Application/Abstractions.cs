using DevAtlas.Domain;

namespace DevAtlas.Application;

public interface IWorkspaceRootStore
{
    Task<IReadOnlyList<WorkspaceRoot>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(WorkspaceRoot root, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetIgnoredAsync(Guid id, bool ignored, CancellationToken cancellationToken = default);
}

public interface IProjectStore
{
    Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default, bool includeUnavailable = false);
    Task SaveAsync(Project project, CancellationToken cancellationToken = default);
    Task ApplyScanAsync(IReadOnlyList<Project> projects, IReadOnlyList<string> completedRoots, CancellationToken cancellationToken = default);
}

public interface IProjectDiscovery
{
    Task<DiscoveryResult> DiscoverAsync(
        IEnumerable<WorkspaceRoot> roots,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record DiscoveryResult(IReadOnlyList<Project> Projects, IReadOnlyList<string> CompletedRoots, IReadOnlyList<string> Warnings);
public sealed record WorkspaceScanResult(IReadOnlyList<Project> Projects, IReadOnlyList<string> Warnings);

public interface IWorkspaceScanner
{
    Task<WorkspaceScanResult> ScanAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

public interface IAppLog
{
    void Write(string message, Exception? exception = null);
}

public interface IGitReader
{
    Task<GitSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default);
}
