using DevAtlas.Domain;

namespace DevAtlas.Application;

public interface IWorkspaceRootStore
{
    Task<IReadOnlyList<WorkspaceRoot>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(WorkspaceRoot root, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IProjectStore
{
    Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(Project project, CancellationToken cancellationToken = default);
}

public interface IProjectDiscovery
{
    IAsyncEnumerable<Project> DiscoverAsync(
        IEnumerable<WorkspaceRoot> roots,
        CancellationToken cancellationToken = default);
}

public interface IGitReader
{
    Task<GitSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default);
}
